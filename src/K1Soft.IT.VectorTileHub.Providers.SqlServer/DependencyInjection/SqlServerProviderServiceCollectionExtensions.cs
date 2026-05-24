using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Providers.SqlServer;

public static class SqlServerProviderServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHubSqlServerProvider(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IVectorTileFeatureProvider, SqlServerFeatureProvider>("SqlServer");
        return services;
    }
}
