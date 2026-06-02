using Hangfire;
using Hangfire.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Jobs;

public static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddVectorTileHubJobs(this IServiceCollection services, IConfiguration configuration)
    {
        // UseConsole() enables per-job log lines and progress bars in the Hangfire dashboard.
        services.AddHangfire(config => config.UseInMemoryStorage().UseConsole());
        services.AddHangfireServer();
        services.AddTransient<CacheGenerationJob>();
        services.AddTransient<CacheDeletionJob>();
        services.AddTransient<CacheInvalidationJob>();
        services.AddTransient<CacheSwapJob>();
        services.AddTransient<CacheTileRefreshJob>();

        // Lets the Core orchestrator enqueue stale-tile refreshes without a Hangfire dependency.
        services.AddSingleton<ITileRefreshQueue, HangfireTileRefreshQueue>();
        return services;
    }
}
