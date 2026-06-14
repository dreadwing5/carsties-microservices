using Carsties.Contracts;
using Carsties.Mapping;
using Carsties.SearchService.Models;

namespace Carsties.SearchService.Mapping;

public static class SearchMappingProfile
{
    public static void Configure(MapperRegistry registry)
    {
        registry.CreateMap<AuctionCreated, Item>(auction => new Item
        {
            ID = auction.Id.ToString(),
            ReservePrice = auction.ReservePrice,
            Seller = auction.Seller,
            Winner = auction.Winner ?? string.Empty,
            SoldAmount = auction.SoldAmount,
            CurrentHighBid = auction.CurrentHighBid,
            CreatedAt = auction.CreatedAt,
            UpdatedAt = auction.UpdatedAt,
            AuctionEnd = auction.AuctionEnd,
            Status = auction.Status,
            Make = auction.Make,
            Model = auction.Model,
            Color = auction.Color,
            Year = auction.Year,
            Mileage = auction.Mileage,
            ImageUrl = auction.ImageUrl,
        });
    }
}
