using Carsties.AuctionService.DTOs;
using Carsties.AuctionService.Entities;
using Carsties.Contracts;
using Carsties.Mapping;

namespace Carsties.AuctionService.Mapping;

public static class AuctionMappingProfile
{
    public static void Configure(MapperRegistry registry)
    {
        registry.CreateMap<Auction, AuctionDto>(auction => new AuctionDto
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
            ImageUrl = auction.Item?.ImageUrl,
        });

        registry.CreateMap<Auction, AuctionCreated>(auction => new AuctionCreated
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
            ImageUrl = auction.Item?.ImageUrl,
        });

        registry.CreateMap<Auction, AuctionUpdated>(auction => new AuctionUpdated
        {
            Id = auction.Id,
            UpdatedAt = auction.UpdatedAt,
            Make = auction.Item?.Make,
            Model = auction.Item?.Model,
            Color = auction.Item?.Color,
            Year = auction.Item?.Year,
            Mileage = auction.Item?.Mileage,
        });

        registry.CreateMap<CreateAuctionDto, Auction>(dto => new Auction
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
                ImageUrl = dto.ImageUrl,
            },
        });
    }
}
