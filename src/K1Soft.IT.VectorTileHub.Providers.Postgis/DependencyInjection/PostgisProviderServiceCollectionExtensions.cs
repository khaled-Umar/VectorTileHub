using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Providers.Postgis;

public static class PostgisProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostGIS feature provider under the keyed name "Postgis". A layer selects it via
    /// <c>"provider": { "type": "Postgis" }</c> in its config.
    /// </summary>
    public static IServiceCollection AddVectorTileHubPostgisProvider(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IVectorTileFeatureProvider, PostgisFeatureProvider>("Postgis");
        return services;
    }
}
