using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class VectorTileHubServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHub(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("VectorTileHub").Get<VectorTileHubOptions>() ?? new VectorTileHubOptions();

        services.AddVectorTileHubCore(configuration);
        services.AddVectorTileHubStorage(configuration);

        if (options.UseMemoryCache)
        {
            services.AddMemoryCache();
            services.AddSingleton<MemoryTileCache>();
        }

        if (options.UseDiskCache)
        {
            services.AddSingleton<DiskTileCache>();
        }

        services.AddSingleton<IVectorTileCache>(sp => new CompositeTileCache(
            options.UseMemoryCache ? sp.GetService<MemoryTileCache>() : null,
            options.UseDiskCache ? sp.GetService<DiskTileCache>() : null));

        services.AddHealthChecks().AddCheck<VectorTileHubHealthCheck>("VectorTileHub");

        // NOTE: the library exposes NO HTTP endpoints. It registers services only; the host owns
        // the HTTP surface (controllers / minimal APIs) and secures it, calling IVectorTileService,
        // IVectorTileLayerConfigProvider and IVectorTileCacheAdmin from its own routes.
        return services;
    }
}
