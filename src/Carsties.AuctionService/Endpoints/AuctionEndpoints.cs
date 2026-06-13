using Carsties.AuctionService.Data;
using Carsties.AuctionService.DTOs;
using Carsties.AuctionService.Entities;
using Carsties.AuctionService.Mapping;
using Carsties.Contracts;
using Carsties.Mapping;
using Carter;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Carsties.AuctionService.Endpoints;

public class AuctionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auctions");

        group.MapGet("", GetAllAuctions)
            .WithName(nameof(GetAllAuctions));

        group.MapGet("/{id:guid}", GetAuctionById)
            .WithName(nameof(GetAuctionById));

        group.MapPost("", CreateAuction)
            .WithName(nameof(CreateAuction));

        group.MapPut("/{id:guid}", UpdateAuction)
            .WithName(nameof(UpdateAuction));

        group.MapDelete("/{id:guid}", DeleteAuction)
            .WithName(nameof(DeleteAuction));
    }

    private static async Task<IResult> GetAllAuctions(AuctionDbContext context, IAppMapper mapper, string date = null)
    {
        var query = context.Auctions.OrderBy(x => x.Item.Make).AsQueryable();

        if (!string.IsNullOrEmpty(date))
        {
            query = query.Where(x => x.UpdatedAt.CompareTo(DateTime.Parse(date).ToUniversalTime()) > 0);
        }

        var auctions = await query.Include(x => x.Item).ToListAsync();

        return Results.Ok(auctions.Select(auction => mapper.Map<AuctionDto>(auction)).ToList());
    }

    private static async Task<IResult> GetAuctionById(AuctionDbContext context, IAppMapper mapper, Guid id)
    {
        var auction = await context.Auctions.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);

        return auction == null
            ? Results.NotFound()
            : Results.Ok(mapper.Map<AuctionDto>(auction));
    }

    private static async Task<IResult> CreateAuction(
        AuctionDbContext context,
        IPublishEndpoint publishEndpoint,
        IAppMapper mapper,
        CreateAuctionDto auctionDto)
    {
        var auction = mapper.Map<Auction>(auctionDto);
        auction.Seller = "test";

        context.Auctions.Add(auction);

        var result = await context.SaveChangesAsync() > 0;

        if (!result)
        {
            return Results.BadRequest("Could not save changes to the DB");
        }

        var newAuction = mapper.Map<AuctionDto>(auction);
        await publishEndpoint.Publish(mapper.Map<AuctionCreated>(newAuction););

        return Results.CreatedAtRoute(
            value: newAuction,
            routeName: nameof(GetAuctionById),
            routeValues: new { id = auction.Id });
    }

    private static async Task<IResult> UpdateAuction(AuctionDbContext context, Guid id, UpdateAuctionDto auctionDto)
    {
        var auction = await context.Auctions.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);

        if (auction == null)
        {
            return Results.NotFound();
        }

        auction.Item.Make = auctionDto.Make ?? auction.Item.Make;
        auction.Item.Model = auctionDto.Model ?? auction.Item.Model;
        auction.Item.Color = auctionDto.Color ?? auction.Item.Color;
        auction.Item.Year = auctionDto.Year ?? auction.Item.Year;
        auction.Item.Mileage = auctionDto.Mileage ?? auction.Item.Mileage;

        var result = await context.SaveChangesAsync() > 0;

        if (!result)
        {
            return Results.BadRequest("Could not save changes to the DB");
        }

        return Results.Ok();
    }

    private static async Task<IResult> DeleteAuction(AuctionDbContext context, Guid id)
    {
        var auction = await context.Auctions.FindAsync(id);

        if (auction == null)
        {
            return Results.NotFound();
        }

        context.Auctions.Remove(auction);

        var result = await context.SaveChangesAsync() > 0;

        if (!result)
        {
            return Results.BadRequest("Could not save changes to the DB");
        }

        return Results.Ok();
    }
}
