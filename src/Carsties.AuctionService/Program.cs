using Carsties.AuctionService.Data;
using Carsties.AuctionService.Mapping;
using Carter;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();
builder.Services.AddValidation();
builder.Services.AddAppMapper();

builder.Services.AddDbContext<AuctionDbContext>(opt =>
{
    _ = opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<AuctionDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(10); // Check if there are messages  that has not been delivered yet
        _ = o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq(
        (context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        }
    );
});

var app = builder.Build();

app.UseHttpsRedirection();

app.MapCarter();

try
{
    DbInitializer.InitDb(app);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

app.Run();

