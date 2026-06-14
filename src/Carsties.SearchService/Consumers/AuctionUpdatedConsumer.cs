using Carsties.Contracts;
using Carsties.Mapping;
using Carsties.SearchService.Models;
using MassTransit;
using MongoDB.Entities;

namespace Carsties.SearchService.Consumers;

public class AuctionUpdatedConsumer(IAppMapper _mapper, ILogger<AuctionUpdatedConsumer> _logger)
    : IConsumer<AuctionUpdated>
{
    public async Task Consume(ConsumeContext<AuctionUpdated> context)
    {
        _logger.LogInformation(
            "Received AuctionUpdated event for auction with ID: {AuctionId}",
            context.Message.Id
        );

        var item = _mapper.Map<Item>(context.Message);

        var result = await DB
            .Default.Update<Item>()
            .Match(x => x.ID == item.ID)
            .ModifyOnly(
                x => new
                {
                    x.Make,
                    x.Model,
                    x.Color,
                    x.Year,
                    x.Mileage,
                    x.UpdatedAt,
                },
                item
            )
            .ExecuteAsync();

        if (!result.IsAcknowledged)
            throw new MessageException(typeof(AuctionUpdated), "Problem updating mongodb");
    }
}
