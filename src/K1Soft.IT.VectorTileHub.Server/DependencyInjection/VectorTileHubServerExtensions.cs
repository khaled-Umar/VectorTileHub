using K1Soft.IT.VectorTileHub.AspNetCore;
using K1Soft.IT.VectorTileHub.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// One-stop facade for hosting VectorTileHub. A single project reference to
/// <c>K1Soft.IT.VectorTileHub.Server</c> brings the whole server-side stack
/// (Core, Storage, AspNetCore, Jobs) transitively; the host adds only its chosen
/// database provider (e.g. <c>AddVectorTileHubSqlServerProvider()</c>).
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
        services.AddVectorTileHub(configuration);     // AspNetCore: core + storage + controllers + health
        services.AddVectorTileHubJobs(configuration); // Jobs: Hangfire
        return services;
    }

    /// <summary>
    /// Pipeline convenience that maps the VectorTileHub endpoints and mounts the Hangfire
    /// dashboard in one call. Pass dashboard authorization filters through to the dashboard
    /// (none = Hangfire's local-only default). Call on a <see cref="WebApplication"/>, which
    /// implements both <see cref="IEndpointRouteBuilder"/> and <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapVectorTileHubServer(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapVectorTileHubEndpoints();
        ((IApplicationBuilder)endpoints).UseVectorTileHubHangfireDashboard();
        return endpoints;
    }
}
