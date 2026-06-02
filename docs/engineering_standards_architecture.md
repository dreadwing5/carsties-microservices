# Engineering Standards & Architecture Patterns

This document outlines the core architectural patterns, deployment strategies, and database management standards for our .NET and React ecosystem.

## 1. Zero-Downtime Deployments & Health Checks

We utilize Azure Deployment Slots combined with a rigorous Health API to ensure deployments never cause production downtime or route traffic to broken code.

### Deployment Slots & Sticky Settings

We maintain strict environment isolation using Azure App Service/Function deployment slots:

* **Staging Slot:** Pointed strictly at the Staging Database using **Slot-Sticky Settings**.
* **Production Slot:** Pointed strictly at the Production Database.

Code is deployed to the Staging slot first. Integration tests run against the staging environment without touching production data. If tests pass, Azure performs a VIP (Virtual IP) swap, instantly routing live traffic to the new code.

### The `/healthz` Gatekeeper

Before Azure swaps the Staging slot to Production, it pings the `GET /healthz` endpoint. This endpoint does not just return `200 OK`; it actively verifies infrastructure.

| Check Type | Target | Failure Action |
| --- | --- | --- |
| **Database** | `SELECT 1` via EF Core | Fails health check, aborts swap |
| **Storage** | Queue permissions & connectivity | Fails health check, aborts swap |
| **Secrets** | Azure Key Vault access | Fails health check, aborts swap |

---

## 2. Database Migration Strategy (DbUp)

We manage database schema and structural data using **DbUp**, executed via a standalone .NET Console project (`Database.csproj`). We do not use Entity Framework Migrations.

### Local Development Isolation

Developers must never share a local database. We treat local databases as disposable resources using Docker.

1. **Boot DB:** `docker compose up -d` spins up an empty SQL Server container.
2. **Migrate:** `dotnet run` on the `Database` project applies all scripts.
3. **Reset:** If schema state gets corrupted, run `docker compose down -v` and restart.

### Migration Script Organization

Scripts are written in raw SQL and must be immutable once merged to `main`. They are organized into three distinct execution folders:

```text
DatabaseProject/
├── Scripts/
│   ├── 01_Schema/       # Tables, Views, Indexes (Prefix: YYYYMMDDHHMM_Name.sql)
│   ├── 02_ConfigSeed/   # Production Lookup Data (Idempotent MERGE/INSERT scripts)
│   └── 03_DevSeed/      # Local Dummy Data (AlwaysRun scripts, wiped & reinserted)

```

**Rule:** Always use timestamps (`YYYYMMDDHHMM_Description.sql`) for schema files to completely eliminate merge conflicts when multiple developers are creating tables simultaneously.

---

## 3. The Natural Key Pattern (Preventing ID Collisions)

To prevent the "Dual-Authority" problem—where local developers and production administrators accidentally generate conflicting integer IDs (`Id = 42`) for configuration data—we strictly separate database relationships from application logic.

### Database Schema (Two-Key Hybrid)

Transactional tables join on integers for performance, but configuration tables expose a unique `VARCHAR` Natural Key for cross-environment synchronization.

| Column | Type | Purpose |
| --- | --- | --- |
| `Id` | `INT IDENTITY(1,1)` | Primary Key. Fast physical sorting and internal Foreign Keys. |
| `ConfigKey` | `VARCHAR(50)` | Unique Natural Key (e.g., `FIRM_CANADA`). |
| `DisplayName` | `VARCHAR(100)` | Human-readable label. |

### C# Smart Enums

Application logic **never** references database integer IDs. We use the Smart Enum pattern to bind business logic to the immutable Natural Key.

```csharp
public sealed class MemberFirm : SmartEnum<MemberFirm, string>
{
    public static readonly MemberFirm USA = new("USA", "FIRM_USA");
    public static readonly MemberFirm Canada = new("Canada", "FIRM_CANADA");

    private MemberFirm(string name, string value) : base(name, value) { }
}

```

---

## 4. Hybrid Localization Strategy

We support multi-language interfaces without bottlenecking the database or complicating API caching. We use a hybrid model based on the origin of the data.

### The API Data Contract

Dropdowns and lookup lists return a standardized DTO supporting both frontend and backend translation:

```json
{
  "code": "FIRM_CANADA",
  "labelKey": "dropdowns.firms.canada",
  "directLabel": null
}

```

### Routing the Translation (React Component)

* **System Data (DbUp Seeded):** Sends a `labelKey`. The React frontend translates it instantly using local `i18n` JSON files. This allows the API to be aggressively cached on a CDN.
* **Admin Data (User Generated):** Sends a `directLabel`. For dynamic data created via the Admin Panel, the .NET Web API handles the translation via SQL joins and returns the raw string.

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

## 5. Minimal APIs & Result Handling

We use **Minimal APIs** as the primary delivery mechanism for our microservices, keeping the HTTP layer lightweight and strictly decoupled from business logic.

### Endpoint Organization
Endpoints must not contain business logic. They act as thin wrappers routing HTTP requests to the Service or Application layer (e.g., MediatR or Repositories). Endpoints should be organized using Extension Methods on `IEndpointRouteBuilder` to prevent `Program.cs` bloat.

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
We use the **Result Pattern** (`FluentResults`) in the Application layer to handle success, failure, and validation without throwing exceptions for control flow.

All `Result<T>` objects returned from the Service layer must be converted to standard HTTP responses using a global extension method (`ToHttpResult`). This ensures consistent JSON structures (like `ValidationProblemDetails`) for the frontend.

#### Standardized API Responses
The API layer maps the `Result` into a predictable structure:
* **Success:** Returns HTTP `200 OK`, `201 Created`, or `204 No Content` with the raw JSON body.
* **Failure (e.g., Validation, NotFound, Forbidden):** Returns the appropriate HTTP status code (`400`, `404`, `403`) mapped to a standard `ProblemDetails` JSON envelope containing the `Errors` array and metadata parameters.

```csharp
// Example Extension Method snippet
public static IResult ToHttpResult<T>(this Result<T> result)
{
    if (result.IsSuccess) return Results.Ok(result.Value);
    
    // Maps internal Enum ErrorType to HTTP Status Codes and ProblemDetails
    return MapErrorsToProblemDetails(result.Errors);
}
```

### Alternative Endpoint Organization: The Carter Library
As an alternative to manual extension methods, we can utilize the **Carter** NuGet package (`Carter`). Carter enforces a standardized structure for Minimal APIs, making them feel as organized as Controllers without sacrificing performance.

When using Carter, endpoints are defined by implementing the `ICarterModule` interface:

```csharp
using Carter;

namespace AuctionService.Endpoints;

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

Carter automatically discovers all classes implementing `ICarterModule` across the assembly, meaning `Program.cs` stays perfectly clean with just a single registration call:

```csharp
// In Program.cs
builder.Services.AddCarter();

var app = builder.Build();
app.MapCarter();
```

---

## 6. Exceptions vs. Result Pattern

To maintain high performance and ensure clear separation between business logic and infrastructure, we enforce strict rules on when to throw Exceptions versus when to return a `Result.Fail`.

### The Golden Rule
* **Use the Result Pattern (`Result.Fail`)** for expected failures (e.g., validation errors, business rule violations, resource not found).
* **Throw Exceptions** only for unexpected technical failures (e.g., database outages, null reference bugs, 500 errors from external APIs).

### Database Operations
Do **not** wrap general database commands in `try/catch` blocks. Let infrastructure exceptions (like timeouts or connection drops) bubble up to the Global Exception Handler to be logged as critical server errors and return a `500 Internal Server Error`.

**The Exception:** You may use `try/catch` specifically to catch known constraint violations (like Unique Index violations) to translate them into friendly business errors:

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
When communicating with other microservices or third-party APIs (like Stripe), handle failures based on the HTTP Status Code:

1. **Infrastructure Failures (5xx or Timeouts):** Use `.EnsureSuccessStatusCode()` to throw an `HttpRequestException`. This allows resilience libraries like **Polly** to catch the exception, trigger retry policies, and eventually bubble up to the Global Exception Handler if the target remains offline.
2. **Business Failures (4xx):** If the external API returns a 400 Bad Request or 422 Unprocessable Entity, do **not** throw. Parse their JSON error response and return a `Result.Fail` so the frontend can display the validation issue to the user.

---

## 7. Dynamic Type Dispatch (Avoiding Reflection)

We strictly prohibit the use of **Reflection** in high-performance or high-throughput areas of the application (such as queue processors or minimal API endpoints) due to its significant performance overhead and incompatibility with AOT (Ahead-of-Time) compilation.

When you need to dynamically route requests to different handlers or database tables based on a runtime value (e.g., routing an incoming queue message to the correct `DbSet<T>`), use one of the following two patterns:

### Pattern A: The DI Handler Registry (Strategy Pattern)
Create an interface (`IRequestHandler`), implement it for each entity, and register all implementations in the DI container. At runtime, use a Dictionary to look up the correct handler in O(1) time. This satisfies the Open-Closed Principle without reflection.

### Pattern B: Source Generators (For maximum performance)
For scenarios requiring raw execution speed, we utilize **Source Generators**. Instead of using reflection to find types at runtime, Source Generators analyze the code at compile-time and automatically generate standard `switch` statements for routing.

#### Viewing Generated Code
A major benefit of Source Generators is transparency. The generated code is standard C# that can be inspected and debugged.
* **In the IDE:** Navigate to `Dependencies -> Analyzers -> [GeneratorName]` in the Solution Explorer to view and place breakpoints in the `.g.cs` files.
* **On Disk:** To force the compiler to save the generated files to the hard drive (for code reviews or inspection), add the following to the `.csproj`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>GeneratedCode</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```
