using Carsties.Contracts;
using Carsties.SearchService.Models;
using MassTransit;
using MongoDB.Entities;

namespace Carsties.SearchService.Consumers;

public class AuctionUpdatedConsumer : IConsumer<AuctionUpdated>
{
    public async Task Consume(ConsumeContext<AuctionUpdated> context)
    {
        var message = context.Message;
        var item = await DB.Default.Find<Item>().OneAsync(message.Id.ToString());

        if (item == null)
        {
            return;
        }

        item.Make = message.Make ?? item.Make;
        item.Model = message.Model ?? item.Model;
        item.Color = message.Color ?? item.Color;
        item.Year = message.Year ?? item.Year;
        item.Mileage = message.Mileage ?? item.Mileage;
        item.UpdatedAt = message.UpdatedAt;

        await DB.Default.SaveAsync(item);
    }
}
