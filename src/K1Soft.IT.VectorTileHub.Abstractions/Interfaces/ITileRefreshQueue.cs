namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Enqueues a background refresh for a single stale tile. Implemented by the Jobs
/// package (Hangfire-backed); the Core orchestrator depends on it optionally so it
/// stays free of any background-job framework dependency. When no implementation is
/// registered, stale tiles are still served — they are simply not refreshed.
/// </summary>
public interface ITileRefreshQueue
{
    void EnqueueTileRefresh(int layerId, string variantKey, int z, int x, int y);
}
