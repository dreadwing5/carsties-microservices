#nullable enable

namespace Carsties.AuctionService.Services;

public interface ICurrentUser
{
    string Username { get; }
}
