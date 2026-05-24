using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Providers.Oracle;

public static class OracleProviderServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHubOracleProvider(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IVectorTileFeatureProvider, OracleFeatureProvider>("Oracle");
        return services;
    }
}
