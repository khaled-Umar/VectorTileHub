using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Jobs;

public static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHubJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config => config.UseInMemoryStorage());
        services.AddHangfireServer();
        services.AddTransient<CacheGenerationJob>();
        services.AddTransient<CacheDeletionJob>();
        services.AddTransient<CacheInvalidationJob>();
        services.AddTransient<CacheSwapJob>();
        return services;
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
