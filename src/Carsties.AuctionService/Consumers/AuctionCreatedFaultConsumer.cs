using Carsties.Contracts;
using MassTransit;

namespace Carsties.AuctionService.Consumers;

public class AuctionCreatedFaultConsumer(ILogger<AuctionCreatedFaultConsumer> logger)
    : IConsumer<Fault<AuctionCreated>>
{
    private readonly ILogger<AuctionCreatedFaultConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<Fault<AuctionCreated>> context)
    {
        _logger.LogInformation("--> Consuming faulty creation");

        //NOTE: This is just an example of how to handle faults. In a real application, you would want to send log information to a logging service or update an error dashboard.

        var exception = context.Message.Exceptions.First();

        if (exception.ExceptionType == "System.ArgumentException")
        {
            context.Message.Message.Model = "FooBar";
            await context.Publish(context.Message.Message);
        }
        else
        {
            _logger.LogInformation("Not an argument exception - update error dashboard somewhere");
        }
    }
}
