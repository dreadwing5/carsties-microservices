namespace Carsties.Mapping;

public class AppMapper(MapperRegistry registry) : IAppMapper
{
    public TDestination Map<TDestination>(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sourceType = source.GetType();
        var destinationType = typeof(TDestination);

        if (!registry.TryGetMap(sourceType, destinationType, out var mapFunc))
        {
            throw new InvalidOperationException(
                $"No map configured from {sourceType.Name} to {destinationType.Name}");
        }

        return (TDestination)mapFunc(source)!;
    }
}
