using Carsties.Contracts;
using Carsties.SearchService.Models;
using MassTransit;
using MongoDB.Entities;

namespace Carsties.SearchService.Consumers;

public class AuctionDeletedConsumer(ILogger<AuctionDeletedConsumer> logger)
    : IConsumer<AuctionDeleted>
{
    public async Task Consume(ConsumeContext<AuctionDeleted> context)
    {
        logger.LogInformation(
            "Received AuctionDeleted event for auction with ID: {AuctionId}",
            context.Message.Id
        );

        var result = await DB.Default.DeleteAsync<Item>(context.Message.Id.ToString());

        if (!result.IsAcknowledged)
            throw new MessageException(typeof(AuctionDeleted), "Problem deleting auction");
    }
}

