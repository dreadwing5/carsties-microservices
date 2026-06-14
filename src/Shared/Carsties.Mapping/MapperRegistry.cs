using System.Diagnostics.CodeAnalysis;

namespace Carsties.Mapping;

public class MapperRegistry
{
    private readonly Dictionary<(Type Source, Type Destination), Func<object, object?>> _maps = [];

    public MapperRegistry CreateMap<TSource, TDestination>(Func<TSource, TDestination> mapFunc)
    {
        ArgumentNullException.ThrowIfNull(mapFunc);

        _maps[(typeof(TSource), typeof(TDestination))] = source => mapFunc((TSource)source);
        return this;
    }

    public bool TryGetMap(
        Type source,
        Type destination,
        [MaybeNullWhen(false)] out Func<object, object?> mapFunc
    )
    {
        return _maps.TryGetValue((source, destination), out mapFunc);
    }
}
