#nullable enable

namespace Carsties.AuctionService.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string Username =>
        httpContextAccessor.HttpContext?.User.Identity?.Name
        ?? throw new InvalidOperationException(
            "The current endpoint requires an authenticated user with a username."
        );
}
