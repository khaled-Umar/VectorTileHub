using Hangfire;

namespace K1Soft.IT.VectorTileHub.Jobs;

/// <summary>
/// Hangfire-backed <see cref="ITileRefreshQueue"/>. Enqueues a background refresh for
/// a single stale tile. Registered by the Jobs package so the Core orchestrator can
/// trigger stale-while-revalidate without referencing Hangfire directly.
/// </summary>
public sealed class HangfireTileRefreshQueue : ITileRefreshQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireTileRefreshQueue(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void EnqueueTileRefresh(int layerId, string variantKey, int z, int x, int y)
    {
        _jobs.Enqueue<CacheTileRefreshJob>(job => job.Execute(layerId, variantKey, z, x, y, CancellationToken.None));
    }
}
