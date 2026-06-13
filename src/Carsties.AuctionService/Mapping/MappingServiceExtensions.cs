using Carsties.Mapping;

namespace Carsties.AuctionService.Mapping;

public static class MappingServiceExtensions
{
    public static IServiceCollection AddAppMapper(this IServiceCollection services)
    {
        var registry = new MapperRegistry();
        AuctionMappingProfile.Configure(registry);

        services.AddSingleton(registry);
        services.AddSingleton<IAppMapper, AppMapper>();

        return services;
    }
}
