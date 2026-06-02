# Source-Generated Request Processing

This document describes a batching and update pattern for a .NET Azure Function that needs to process pending work across multiple transaction entity types.

The goal is to avoid:

- Polling many transaction tables to discover work.
- Calling an external API once per transaction.
- Writing duplicated EF Core update code for each transaction table.
- Maintaining switch statements or dictionaries in multiple places.
- Storing fragile CLR/domain class names in Azure Table Storage.

## Problem

The current workflow has a timer-triggered function that:

1. Queries multiple transaction tables.
2. Finds records that need external API enrichment.
3. Calls an external API.
4. Joins API results back to the original records.
5. Updates the transaction tables.

This becomes messy when there are many transaction tables with the same fields. Even if the update code is generic, the function still needs a clean way to identify which table/entity type each pending item belongs to.

## Proposed Architecture

Use Azure Table Storage as a lightweight **work item and batching layer**, while keeping the relational database as the source of truth.

```text
Azure Table Storage
  Stores pending work items and processing status.

External API
  Returns data for multiple work items in one batch call.

SQL Database
  Stores the actual transaction entities.

Azure Function
  Claims pending work, calls the API, updates SQL through EF Core, then updates Azure Table status.
```

Azure Table Storage should store stable logical entity names:

```text
EntityType = Invoice
EntityType = Payment
EntityType = Refund
```

It should not store CLR type names:

```text
MyApp.Domain.InvoiceTransaction
MyApp.Domain.PaymentTransaction, MyApp.Domain
```

This keeps Azure Table data independent from class, namespace, and assembly refactors.

## Status Flow

Use explicit statuses for safe processing:

```text
Pending -> Processing -> Completed
Pending -> Processing -> Failed
Pending -> Processing -> Pending retry
```

Recommended Azure Table fields:

```text
PartitionKey
RowKey
EntityType
EntityId
ApiBatchKey
Status
RetryCount
LockedUntil
LastAttemptAt
CompletedAt
LastError
```

The timer function should only process rows it successfully claims as `Processing`.

## Azure Table Claiming

Azure Table Storage uses `ETag` values for optimistic concurrency. When claiming a row, update it from `Pending` to `Processing` using the `ETag` from the read operation.

If another function instance updated the same row first, Azure Table returns a precondition failure and the current worker should skip that row.

Conceptual flow:

```csharp
foreach (var item in pendingItems)
{
    item.Status = "Processing";
    item.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(10);
    item.LastAttemptAt = DateTimeOffset.UtcNow;

    try
    {
        await tableClient.UpdateEntityAsync(
            item,
            item.ETag,
            TableUpdateMode.Replace,
            cancellationToken);

        claimedItems.Add(item);
    }
    catch (RequestFailedException ex) when (ex.Status == 412)
    {
        // Another worker claimed or changed this item first.
    }
}
```

Use `LockedUntil` so stuck `Processing` rows can be retried if the function crashes.

## Database Update Flow

After work items are claimed:

1. Group work items by `ApiBatchKey`.
2. Call the external API once per group.
3. Group API results by `EntityType`.
4. Query the matching EF Core table once per entity type.
5. Update entity values in memory.
6. Call `SaveChangesAsync` once for the batch or once per API group.
7. Mark Azure Table rows `Completed` or `Failed`.

The important distinction:

```text
Good:
  Query table once.
  Update entities in memory.
  Save once.

Bad:
  For each item:
    Query database.
    Save database changes.
```

EF Core still generally generates separate `UPDATE` statements per changed row, but `SaveChangesAsync` batches those statements into fewer database roundtrips. For around 1000 rows, this is usually acceptable. If the volume grows much larger, consider raw SQL, table-valued parameters, temporary staging tables, SQL `MERGE`, or a bulk update library.

## Common Entity Interface

All transaction entities that participate in this workflow should implement a common interface:

```csharp
public interface IRequestEntity
{
    Guid Id { get; }

    string? AttachmentUrl { get; set; }

    string? ExternalStatus { get; set; }

    DateTime? ExternalApiProcessedAt { get; set; }

    DateTime UpdatedAt { get; set; }
}
```

Example entity:

```csharp
[RequestEntityType("Invoice")]
public sealed class InvoiceTransaction : IRequestEntity
{
    public Guid Id { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? ExternalStatus { get; set; }

    public DateTime? ExternalApiProcessedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

## Attribute-Based Entity Key

Use an attribute to connect a stable Azure Table `EntityType` value to a domain entity:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequestEntityTypeAttribute : Attribute
{
    public RequestEntityTypeAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
```

The value should be treated as a stable contract:

```csharp
[RequestEntityType("Invoice")]
```

Class names can change. The logical key should rarely change once records have been written to Azure Table Storage.

## Why Source Generation

A runtime registry can scan assemblies and build this map:

```text
Invoice -> InvoiceTransaction
Payment -> PaymentTransaction
Refund -> RefundTransaction
```

That works, but it still creates a runtime `Type`. Calling a generic method such as `UpdateAsync<TEntity>` from a runtime `Type` usually requires reflection.

A source generator solves this by generating normal C# code at build time:

```csharp
entityType switch
{
    "Invoice" => RequestEntityUpdater.UpdateAsync<InvoiceTransaction>(...),
    "Payment" => RequestEntityUpdater.UpdateAsync<PaymentTransaction>(...),
    "Refund" => RequestEntityUpdater.UpdateAsync<RefundTransaction>(...),
    _ => throw new InvalidOperationException(...)
}
```

This gives us:

- No hand-written switch statement in business code.
- No repeated dictionaries.
- No `Type.GetType`.
- No Azure Table dependency on CLR names.
- No runtime generic reflection invocation.
- Compile-time validation for duplicate or invalid entity keys.

## Codebase Structure

Recommended structure:

```text
src/
  MyApp.Domain/
    IRequestEntity.cs
    RequestEntityTypeAttribute.cs
    Entities/
      InvoiceTransaction.cs
      PaymentTransaction.cs
      RefundTransaction.cs

  MyApp.Infrastructure/
    AppDbContext.cs
    RequestEntityUpdater.cs

  MyApp.Functions/
    ProcessPendingRequestsFunction.cs
    WorkItem.cs
    ApiResult.cs

  MyApp.RequestProcessing.SourceGenerators/
    MyApp.RequestProcessing.SourceGenerators.csproj
    RequestEntityUpdateDispatcherGenerator.cs
```

## Naming Conventions

Recommended names:

| Concern | Name |
| --- | --- |
| Source generator project | `MyApp.RequestProcessing.SourceGenerators` |
| Generator class | `RequestEntityUpdateDispatcherGenerator` |
| Attribute class | `RequestEntityTypeAttribute` |
| Generated class | `RequestEntityUpdateDispatcher` |
| Generated file | `RequestEntityUpdateDispatcher.g.cs` |
| Generic updater | `RequestEntityUpdater` |

The `.g.cs` suffix is commonly used for generated C# files.

## Generic EF Core Updater

The generic updater is handwritten:

```csharp
public static class RequestEntityUpdater
{
    public static async Task<IReadOnlyCollection<Guid>> UpdateAsync<TEntity>(
        AppDbContext db,
        IReadOnlyCollection<WorkItem> workItems,
        IReadOnlyDictionary<Guid, ApiResult> apiResults,
        CancellationToken cancellationToken)
        where TEntity : class, IRequestEntity
    {
        var ids = workItems
            .Select(x => x.EntityId)
            .Distinct()
            .ToList();

        var entities = await db.Set<TEntity>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var updatedIds = new List<Guid>();
        var now = DateTime.UtcNow;

        foreach (var entity in entities)
        {
            if (!apiResults.TryGetValue(entity.Id, out var result))
            {
                continue;
            }

            entity.AttachmentUrl = result.AttachmentUrl;
            entity.ExternalStatus = result.Status;
            entity.ExternalApiProcessedAt = now;
            entity.UpdatedAt = now;

            updatedIds.Add(entity.Id);
        }

        return updatedIds;
    }
}
```

## Generated Dispatcher

The source generator produces code similar to this:

```csharp
public static partial class RequestEntityUpdateDispatcher
{
    public static Task<IReadOnlyCollection<Guid>> UpdateAsync(
        string entityType,
        AppDbContext db,
        IReadOnlyCollection<WorkItem> workItems,
        IReadOnlyDictionary<Guid, ApiResult> apiResults,
        CancellationToken cancellationToken)
    {
        return entityType switch
        {
            "Invoice" => RequestEntityUpdater.UpdateAsync<InvoiceTransaction>(
                db,
                workItems,
                apiResults,
                cancellationToken),

            "Payment" => RequestEntityUpdater.UpdateAsync<PaymentTransaction>(
                db,
                workItems,
                apiResults,
                cancellationToken),

            "Refund" => RequestEntityUpdater.UpdateAsync<RefundTransaction>(
                db,
                workItems,
                apiResults,
                cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unknown request entity type: {entityType}")
        };
    }
}
```

The application code calls the dispatcher. Developers do not manually edit this generated class.

## Timer Function Shape

The timer-triggered function stays focused on workflow:

```csharp
public async Task RunAsync(CancellationToken cancellationToken)
{
    var pendingItems = await azureTable.GetPendingAsync(cancellationToken);

    var claimedItems = await azureTable.ClaimAsProcessingAsync(
        pendingItems,
        cancellationToken);

    var completedIds = new List<Guid>();

    foreach (var apiGroup in claimedItems.GroupBy(x => x.ApiBatchKey))
    {
        var apiResults = await externalApi.GetResultsAsync(
            apiGroup.Key,
            cancellationToken);

        foreach (var entityGroup in apiGroup.GroupBy(x => x.EntityType))
        {
            var updatedIds = await RequestEntityUpdateDispatcher.UpdateAsync(
                entityGroup.Key,
                db,
                entityGroup.ToList(),
                apiResults,
                cancellationToken);

            completedIds.AddRange(updatedIds);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    await azureTable.MarkCompletedAsync(completedIds, cancellationToken);
}
```

## Source Generator Project Reference

The consuming project references the generator as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\MyApp.RequestProcessing.SourceGenerators\MyApp.RequestProcessing.SourceGenerators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generator project is committed to git. It is part of the source code.

## Should Generated Code Be Committed?

Usually, no.

Commit:

```text
MyApp.RequestProcessing.SourceGenerators.csproj
RequestEntityUpdateDispatcherGenerator.cs
```

Do not normally commit:

```text
RequestEntityUpdateDispatcher.g.cs
```

Generated files are produced during build. To inspect them locally, add this to the consuming project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Then add the output folder to `.gitignore`:

```gitignore
Generated/
```

Commit generated output only if the team has a specific policy requiring generated files to be reviewed or if the build environment cannot run source generators.

## Validation Rules

The generator should fail the build when:

- A class has `[RequestEntityType]` but does not implement `IRequestEntity`.
- Two classes use the same entity key.
- An entity key is null, empty, or whitespace.
- A supported entity type is not part of the EF Core model, if the generator can validate that reliably.

Failing at build time is better than discovering bad mappings during a timer run.

## Reliability Notes

Because Azure Table Storage and SQL are separate systems, this flow is not one atomic transaction.

Example partial failure:

```text
SQL update succeeds.
Azure Table update to Completed fails.
```

The item may be retried later. Therefore, processing should be idempotent:

- Updating the same attachment/status twice should be safe.
- SQL rows should track `ExternalApiProcessedAt` or an equivalent marker.
- Azure Table rows should use `RetryCount`, `LockedUntil`, and `LastError`.

## Recommendation

Use this pattern when the workflow is important enough to justify source generator complexity:

```text
Azure Table:
  stores stable logical work item keys.

Domain entities:
  implement IRequestEntity and declare [RequestEntityType("Invoice")].

Source generator:
  creates RequestEntityUpdateDispatcher.g.cs at build time.

Azure Function:
  claims pending work, batches API calls, calls generated dispatcher, saves EF changes, updates Azure Table status.
```

If the team wants a simpler first version, start with an attribute-based runtime registry using the same `[RequestEntityType]` attributes. That keeps the domain model compatible with a future source generator.

## References

- EF Core `DbContext.Set<TEntity>()`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontext.set
- EF Core efficient updating and batching: https://learn.microsoft.com/en-us/ef/core/performance/efficient-updating
- Azure Table Storage `ETag` optimistic concurrency: https://learn.microsoft.com/en-us/rest/api/storageservices/update-entity2
- .NET source generators overview: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview
