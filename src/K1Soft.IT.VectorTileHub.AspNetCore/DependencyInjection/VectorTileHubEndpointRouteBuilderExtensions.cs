using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class VectorTileHubEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapVectorTileHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<VectorTileHubOptions>>().Value;
        endpoints.MapControllers();
        endpoints.MapHealthChecks(options.HealthCheckPath);
        return endpoints;
    }

    /// <summary>
    /// Mounts the Hangfire dashboard. The library enforces NO built-in access policy —
    /// the host supplies its own authorization filters (none = Hangfire's default,
    /// which permits local requests only).
    /// </summary>
    public static IApplicationBuilder UseVectorTileHubHangfireDashboard(
        this IApplicationBuilder app,
        params IDashboardAuthorizationFilter[] authorizationFilters)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<VectorTileHubOptions>>().Value;
        if (!options.Hangfire.Enabled)
        {
            return app;
        }

        var dashboardOptions = new DashboardOptions();
        if (authorizationFilters is { Length: > 0 })
        {
            dashboardOptions.Authorization = authorizationFilters;
        }

        return app.UseHangfireDashboard(options.Hangfire.DashboardPath, dashboardOptions);
    }
}
