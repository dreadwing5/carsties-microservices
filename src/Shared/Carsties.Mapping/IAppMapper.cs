namespace Carsties.Mapping;

public interface IAppMapper
{
    TDestination Map<TDestination>(object source);
}
