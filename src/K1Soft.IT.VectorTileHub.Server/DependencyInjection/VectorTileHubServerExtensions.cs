using K1Soft.IT.VectorTileHub.AspNetCore;
using K1Soft.IT.VectorTileHub.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// One-stop facade for <em>registering</em> the VectorTileHub server stack. A single project
/// reference to <c>K1Soft.IT.VectorTileHub.Server</c> brings the whole server-side stack
/// (Core, Storage, AspNetCore, Jobs) transitively; the host adds only its chosen database
/// provider (e.g. <c>AddVectorTileHubSqlServerProvider()</c>).
///
/// <para>The library exposes NO HTTP endpoints. The host owns the HTTP surface: it injects
/// <c>IVectorTileService</c>, <c>IVectorTileLayerConfigProvider</c> and
/// <c>IVectorTileCacheAdmin</c> into its own (authorized) controllers, and opts into the
/// Hangfire dashboard via <c>UseVectorTileHubHangfireDashboard(...)</c>.</para>
/// </summary>
public static class VectorTileHubServerExtensions
{
    /// <summary>
    /// Registers the full VectorTileHub server stack: core services + storage (via
    /// <see cref="VectorTileHubServiceCollectionExtensions.AddVectorTileHub"/>) and the
    /// Hangfire cache jobs. The data provider is intentionally NOT registered here — the
    /// host adds its own (e.g. <c>services.AddVectorTileHubSqlServerProvider()</c>).
    /// </summary>
    public static IServiceCollection AddVectorTileHubServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddVectorTileHub(configuration);     // AspNetCore: core + storage + health (services only)
        services.AddVectorTileHubJobs(configuration); // Jobs: Hangfire + IVectorTileCacheAdmin
        return services;
    }
}
