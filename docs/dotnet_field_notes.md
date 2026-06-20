# .NET Field Notes

Small .NET and C# lessons collected while working on the project. These notes focus on useful idioms, common gotchas, and the reasoning behind everyday APIs.

## Guard Clauses

### `ArgumentNullException.ThrowIfNull`

Use `ArgumentNullException.ThrowIfNull` when a method or constructor cannot accept a null argument:

```csharp
public AuctionService(IAuctionRepository repository)
{
    ArgumentNullException.ThrowIfNull(repository);
    _repository = repository;
}
```

It is the concise equivalent of:

```csharp
if (repository is null)
{
    throw new ArgumentNullException(nameof(repository));
}
```

#### Benefits

- Fails immediately at the method boundary with a clear exception.
- Removes repetitive null-checking boilerplate.
- Automatically captures the argument expression, so `nameof(repository)` is normally unnecessary.
- Informs nullable-flow analysis that the value is non-null after the guard clause.
- Makes constructor and public API requirements explicit at runtime.

The argument expression is captured using `CallerArgumentExpression`. A parameter name can still be supplied explicitly, but that is rarely needed:

```csharp
ArgumentNullException.ThrowIfNull(repository, nameof(repository));
```

#### When to use it

Use it for invalid null input supplied by a caller, especially at public method and constructor boundaries.

```csharp
public Task CreateAuctionAsync(Auction auction)
{
    ArgumentNullException.ThrowIfNull(auction);

    // The compiler and the reader now know auction is non-null.
    return SaveAsync(auction);
}
```

#### What it does not replace

It does not cover other validation rules, such as:

- Empty or whitespace-only strings.
- Numbers outside an allowed range.
- Invalid business state.
- Missing database records.

Use the validation or exception type that describes those failures accurately.

#### Nullable reference types still matter

Prefer a non-nullable parameter when null is not part of the contract:

```csharp
public void CreateAuction(Auction auction)
```

Nullable reference types provide compile-time guidance. `ThrowIfNull` adds runtime protection for callers that bypass or do not use nullable analysis. The two mechanisms complement each other.

## Invariants

An invariant is a condition that the application guarantees will be true at a particular point in the code.

For example, an authorization policy can establish this invariant:

> If the endpoint handler is running, the request is authenticated and has a username.

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserWithUsername", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            !string.IsNullOrWhiteSpace(context.User.Identity?.Name));
    });
});
```

Apply the policy at the endpoint boundary:

```csharp
app.MapPost("/api/auctions", CreateAuction)
    .RequireAuthorization("UserWithUsername");
```

The handler can then use the username directly without repeating the same null or whitespace check:

```csharp
auction.Seller = currentUser.Username;
```

The policy owns the validation, while the handler relies on the established invariant. This reduces duplication and makes the handler's assumptions explicit.
