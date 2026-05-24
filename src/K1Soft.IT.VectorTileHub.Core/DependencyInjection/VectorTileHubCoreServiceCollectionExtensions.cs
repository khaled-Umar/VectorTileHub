using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub;

public static class VectorTileHubCoreServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHubCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VectorTileHubOptions>(configuration.GetSection("VectorTileHub"));
        services.AddSingleton<IVectorTileLayerConfigProvider, JsonLayerConfigProvider>();
        services.AddSingleton<IVectorTileEncoder, MapboxVectorTileEncoder>();
        services.AddScoped<IVectorTileSecurityScopeResolver, DefaultSecurityScopeResolver>();
        services.AddScoped<IVectorTileService, VectorTileOrchestrator>();
        return services;
    }
}
