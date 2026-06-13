using Carsties.SearchService.Mapping;
using Carter;
using MassTransit;
using MongoDB.Driver;
using MongoDB.Entities;
using Polly;
using Polly.Extensions.Http;
using Carsties.SearchService.Consumers;
using Carsties.SearchService.Data;
using Carsties.SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();
builder.Services.AddValidation();
builder.Services.AddAppMapper();
builder.Services.AddHttpClient<AuctionSvcHttpClient>().AddPolicyHandler(GetPolicy());

builder.Services.AddMassTransit(x =>
{
    x.AddConsumersFromNamespaceContaining<AuctionCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("search", false));
    });
});
var app = builder.Build();

static IAsyncPolicy<HttpResponseMessage> GetPolicy()
    => HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(3), (outcome, retryCount, timeSpan) => 
        {
            Console.WriteLine($"Polly is retrying HTTP request. Carsties.AuctionService might be down. Waiting {timeSpan.TotalSeconds} seconds...");
            return Task.CompletedTask;
        });

app.UseHttpsRedirection();

app.Lifetime.ApplicationStarted.Register(async () => 
{
    try
    {
        await DbInitializer.InitDb(app);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
});

app.MapCarter();

app.Run();
