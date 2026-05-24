using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class VectorTileHubEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapVectorTileHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<VectorTileHubOptions>>().Value;
        endpoints.MapTileEndpoints(options);
        endpoints.MapLayerMetadataEndpoints(options);
        endpoints.MapAdminCacheEndpoints(options);
        endpoints.MapAdminConfigEndpoints(options);
        endpoints.MapHealthChecks(options.HealthCheckPath);
        return endpoints;
    }

    public static IApplicationBuilder UseVectorTileHubHangfireDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<VectorTileHubOptions>>().Value;
        if (!options.Hangfire.Enabled)
        {
            return app;
        }

        return app.UseHangfireDashboard(options.Hangfire.DashboardPath, new DashboardOptions
        {
            Authorization = [new RoleDashboardAuthorizationFilter(options.Hangfire.RequiredRoles)]
        });
    }

    private sealed class RoleDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly string[] _roles;

        public RoleDashboardAuthorizationFilter(string[] roles)
        {
            _roles = roles;
        }

        public bool Authorize(DashboardContext context)
        {
            var user = context.GetHttpContext().User;
            return user.Identity?.IsAuthenticated == true && (_roles.Length == 0 || _roles.Any(user.IsInRole));
        }
    }
}
