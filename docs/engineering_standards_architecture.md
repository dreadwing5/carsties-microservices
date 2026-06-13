# Engineering Standards and Architecture Patterns

This document collects engineering patterns that are useful for the broader Carsties microservices project.

Some sections are future-facing reference material. The current tutorial services use Minimal APIs with Carter, EF Core migrations, Postgres, MongoDB, RabbitMQ, and Docker Compose. See [Asynchronous communication](asynchronous_communication.md) for the dedicated RabbitMQ and MassTransit notes.

## Zero-Downtime Deployments and Health Checks

For production deployments, use deployment slots and health checks to avoid routing traffic to broken code.

### Deployment Slots and Sticky Settings

When deploying to Azure App Service or Azure Functions:

- **Staging slot:** Points to the staging database using slot-sticky settings.
- **Production slot:** Points to the production database.

Deploy code to staging first, run integration checks there, then swap staging into production only after the service is healthy.

### `/healthz` Gatekeeper

Before a slot swap, the platform should call `GET /healthz`. The endpoint should verify infrastructure, not just return `200 OK`.

| Check Type | Target | Failure Action |
| --- | --- | --- |
| **Database** | `SELECT 1` via EF Core | Fails health check, aborts swap |
| **Storage or broker** | Queue permissions and connectivity | Fails health check, aborts swap |
| **Secrets** | Azure Key Vault access | Fails health check, aborts swap |

---

## Database Migration Strategy

The current tutorial service uses **EF Core migrations** for `Carsties.AuctionService`. Keep migrations small, reviewable, and tied to the service that owns the database.

### Local Development Isolation

Developers should not share local databases. Treat local databases as disposable resources managed by Docker Compose.

1. **Boot databases:** `docker compose up -d` starts Postgres, MongoDB, and RabbitMQ.
2. **Apply migrations:** Run the service or use `dotnet ef database update` for EF Core-backed services.
3. **Reset local state:** If schema or data state gets corrupted, run `docker compose down -v` and start again.

### DbUp Reference Pattern

DbUp can still be useful for larger systems where schema changes and seed/config data should be managed by a standalone database project. If adopted later, use timestamped SQL scripts and keep merged scripts immutable.

Recommended organization:

```text
DatabaseProject/
├── Scripts/
│   ├── 01_Schema/       # Tables, Views, Indexes (Prefix: YYYYMMDDHHMM_Name.sql)
│   ├── 02_ConfigSeed/   # Production Lookup Data (Idempotent MERGE/INSERT scripts)
│   └── 03_DevSeed/      # Local Dummy Data (AlwaysRun scripts, wiped & reinserted)

```

Use timestamps (`YYYYMMDDHHMM_Description.sql`) for schema files to reduce merge conflicts when multiple developers add database changes.

---

## Natural Key Pattern

Use natural keys for cross-environment configuration data to avoid ID collisions.

This avoids the "dual authority" problem where local developers and production administrators can accidentally generate conflicting integer IDs for configuration records.

### Two-Key Hybrid Schema

Transactional tables join on integers for performance, but configuration tables expose a unique `VARCHAR` Natural Key for cross-environment synchronization.

| Column | Type | Purpose |
| --- | --- | --- |
| `Id` | `INT IDENTITY(1,1)` | Primary Key. Fast physical sorting and internal Foreign Keys. |
| `ConfigKey` | `VARCHAR(50)` | Unique Natural Key (e.g., `FIRM_CANADA`). |
| `DisplayName` | `VARCHAR(100)` | Human-readable label. |

### C# Smart Enums

Application logic should not reference database integer IDs for configuration values. Use the Smart Enum pattern to bind business logic to immutable natural keys.

```csharp
public sealed class MemberFirm : SmartEnum<MemberFirm, string>
{
    public static readonly MemberFirm USA = new("USA", "FIRM_USA");
    public static readonly MemberFirm Canada = new("Canada", "FIRM_CANADA");

    private MemberFirm(string name, string value) : base(name, value) { }
}

```

---

## Hybrid Localization Strategy

For multi-language interfaces, use a hybrid model based on the origin of the data.

### API Data Contract

Dropdowns and lookup lists return a standardized DTO supporting both frontend and backend translation:

```json
{
  "code": "FIRM_CANADA",
  "labelKey": "dropdowns.firms.canada",
  "directLabel": null
}

```

### Routing Translation

- **System data:** Sends a `labelKey`. The React frontend translates it with local `i18n` JSON files, which keeps API responses cache-friendly.
- **Admin data:** Sends a `directLabel`. Dynamic data created by users can be translated or returned directly by the API.

```tsx
const SmartDropdown = ({ options }) => {
  const { t } = useTranslation();

  return (
    <select>
      {options.map(opt => (
        <option key={opt.code} value={opt.code}>
          {opt.labelKey ? t(opt.labelKey) : opt.directLabel}
        </option>
      ))}
    </select>
  );
};

```

---

## Minimal APIs and Result Handling

The current services use ASP.NET Core controllers. If we move to Minimal APIs later, keep the HTTP layer lightweight and delegate business logic to the service or application layer.

### Endpoint Organization

Endpoints should not contain business logic. They should route HTTP requests to the service or application layer.

For Minimal APIs, organize endpoints with extension methods on `IEndpointRouteBuilder` to keep `Program.cs` focused on application setup.

```csharp
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");
        group.MapPost("/", CreateOrder);
    }

    private static async Task<IResult> CreateOrder(CreateOrderDto dto, IOrderService service)
    {
        var result = await service.CreateOrderAsync(dto);
        return result.ToHttpResult();
    }
}
```

### The Result Pattern (FluentResults)

When using the Result Pattern, return `Result<T>` from the application layer for expected success/failure flows. Convert those results to HTTP responses in the API layer with a shared extension such as `ToHttpResult`.

### Standardized API Responses

The API layer should map results into predictable HTTP responses:

- **Success:** Return `200 OK`, `201 Created`, or `204 No Content`.
- **Failure:** Return an appropriate status code such as `400`, `403`, or `404` using a standard `ProblemDetails` envelope.

```csharp
// Example Extension Method snippet
public static IResult ToHttpResult<T>(this Result<T> result)
{
    if (result.IsSuccess) return Results.Ok(result.Value);

    // Maps internal Enum ErrorType to HTTP Status Codes and ProblemDetails
    return MapErrorsToProblemDetails(result.Errors);
}
```

### Carter Reference

Carter is an optional Minimal API organization library. Endpoints are defined by implementing `ICarterModule`:

```csharp
using Carter;

namespace Carsties.AuctionService.Endpoints;

public class AuctionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auctions");

        // Keep business logic delegated to the Service/MediatR layer
        group.MapPost("/", async (CreateAuctionDto dto, IAuctionService service) =>
        {
            var result = await service.CreateAuctionAsync(dto);
            return result.ToHttpResult();
        });
    }
}
```

Carter discovers modules across the assembly, keeping `Program.cs` clean:

```csharp
// In Program.cs
builder.Services.AddCarter();

var app = builder.Build();
app.MapCarter();
```

---

## Exceptions vs. Result Pattern

Use exceptions for unexpected technical failures and the Result Pattern for expected business outcomes.

### Golden Rule

- **Use `Result.Fail`:** Validation errors, business rule violations, resource not found, forbidden actions.
- **Throw exceptions:** Database outages, null reference bugs, infrastructure timeouts, unexpected external API failures.

### Database Operations

Do not wrap general database commands in broad `try/catch` blocks. Let infrastructure exceptions, such as timeouts or connection drops, bubble up to the global exception handler.

The exception is a known constraint violation that should become a friendly business error:

```csharp
public async Task<Result<User>> CreateUserAsync(User user)
{
    try
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Result.Ok(user);
    }
    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
    {
        // Translate a known DB constraint into a friendly business error
        return Result.Fail(new AppError(ErrorType.Conflict, "Username is already taken."));
    }
    // All other DB exceptions will naturally bubble up
}
```

### External API Calls (HTTP Clients)

When communicating with other services, handle failures based on the HTTP status code:

1. **Infrastructure failures:** For `5xx`, timeouts, or connection failures, throw an `HttpRequestException` so Polly or the global exception handler can process it.
2. **Business failures:** For expected `4xx` responses, parse the error response and return `Result.Fail` so the frontend can show a useful message.

---

## Dynamic Type Dispatch

Avoid reflection in high-throughput areas such as queue processors and hot HTTP paths. Reflection can add overhead and complicate NativeAOT compatibility.

When routing dynamically based on runtime values, prefer explicit dispatch.

### Pattern A: DI Handler Registry

Create an interface, implement it for each entity or message type, and register all implementations in DI. At runtime, use a dictionary to look up the correct handler.

### Pattern B: Source Generators

For maximum performance, source generators can analyze code at compile time and generate explicit `switch` statements or lookup tables.

#### Viewing Generated Code

Generated code is standard C# that can be inspected and debugged.

- **In the IDE:** Navigate to `Dependencies -> Analyzers -> [GeneratorName]` in Solution Explorer.
- **On disk:** To save generated files for review, add this to the `.csproj`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>GeneratedCode</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```
