using Carsties.Mapping;

namespace Carsties.SearchService.Mapping;

public static class MappingServiceExtensions
{
    public static IServiceCollection AddAppMapper(this IServiceCollection services)
    {
        var registry = new MapperRegistry();
        SearchMappingProfile.Configure(registry);

        services.AddSingleton(registry);
        services.AddSingleton<IAppMapper, AppMapper>();

        return services;
    }
}
