using Carsties.Mapping;
using Carsties.Contracts;
using MassTransit;
using MongoDB.Entities;
using Carsties.SearchService.Models;

namespace Carsties.SearchService.Consumers;

public class AuctionCreatedConsumer(IAppMapper mapper) : IConsumer<AuctionCreated>
{
    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        await DB.Default.SaveAsync(mapper.Map<Item>(context.Message));
    }
}
