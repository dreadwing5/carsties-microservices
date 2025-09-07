using AuctionService.DTOs;
using AuctionService.Entities;

namespace AuctionService.Extensions;

public static class AuctionExtensions
{
    public static AuctionDto ToDto(this Auction auction)
    {
        return new AuctionDto
        {
            Id = auction.Id,
            ReservePrice = auction.ReservePrice,
            Seller = auction.Seller,
            Winner = auction.Winner,
            SoldAmount = auction.SoldAmount,
            CurrentHighBid = auction.CurrentHighBid,
            CreatedAt = auction.CreatedAt,
            UpdatedAt = auction.UpdatedAt,
            AuctionEnd = auction.AuctionEnd,
            Status = auction.Status.ToString(),
            Make = auction.Item?.Make,
            Model = auction.Item?.Model,
            Color = auction.Item?.Color,
            Year = auction.Item?.Year ?? 0,
            Mileage = auction.Item?.Mileage ?? 0,
            ImageUrl = auction.Item?.ImageUrl
        };
    }

    public static List<AuctionDto> ToDtoList(this List<Auction> auctions)
    {
        return [.. auctions.Select(a => a.ToDto())];
    }

    public static Auction ToEntity(this CreateAuctionDto dto)
    {
        return new Auction
        {
            ReservePrice = dto.ReservePrice,
            AuctionEnd = dto.AuctionEnd,
            Status = Status.Live,
            Item = new Item
            {
                Make = dto.Make,
                Model = dto.Model,
                Color = dto.Color,
                Year = dto.Year,
                Mileage = dto.Mileage,
                ImageUrl = dto.ImageUrl
            }
        };
    }
}