using Carsties.Contracts;
using Carsties.Mapping;
using Carsties.SearchService.Models;
using MassTransit;
using MongoDB.Entities;

namespace Carsties.SearchService.Consumers;

public class AuctionCreatedConsumer(IAppMapper _mapper) : IConsumer<AuctionCreated>
{
    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        var item = _mapper.Map<Item>(context.Message);

        // if (item.Model == "Foo")
        //     throw new ArgumentException("Cannot sell cars with the name of Foo"); //NOTE: This is for demonstration purposes only to show  how failures are handled in Masstransit.

        await DB.Default.SaveAsync(item); // NOTE: What if this fails?
    }
}
