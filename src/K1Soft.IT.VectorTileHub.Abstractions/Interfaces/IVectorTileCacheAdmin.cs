namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Cache administration operations (generate / delete / invalidate / notify-change / swap / status)
/// surfaced as an injectable service so a host can expose them from its own controllers with its own
/// authorization. The library no longer ships admin endpoints — the host owns the HTTP surface.
/// Bounding boxes are passed as primitives (no NetTopologySuite dependency on the public contract).
/// </summary>
public interface IVectorTileCacheAdmin
{
    /// <summary>Enqueues a background cache-generation job. Returns the Hangfire job id.</summary>
    string EnqueueGenerate(int layerId, int? minZoom, int? maxZoom, string[]? variants, int? maxDegreeOfParallelism);

    /// <summary>Enqueues a background cache-deletion job. Returns the Hangfire job id.</summary>
    string EnqueueDelete(int layerId, string? cacheVersion, bool deleteAllVersions);

    /// <summary>Enqueues a background change-notification (refresh) job for a bounding box. Returns the Hangfire job id.</summary>
    string EnqueueNotifyChange(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? variants);

    /// <summary>Enqueues a background cache-version swap job. Returns the job id and the resolved version.</summary>
    CacheSwapResult EnqueueSwap(int layerId, string? newVersion, bool regenerateAfterSwap, bool deleteOldVersion);

    /// <summary>
    /// Synchronously removes cached tiles intersecting the bounding box across the affected zoom range
    /// and variants, then records the invalidation time. Returns <c>null</c> when the layer is unknown.
    /// </summary>
    Task<CacheInvalidateResult?> InvalidateAsync(
        int layerId,
        double minX,
        double minY,
        double maxX,
        double maxY,
        int srid,
        string[]? variants,
        CancellationToken cancellationToken);

    /// <summary>Returns the layer's runtime/operational state, or <c>null</c> when none has been recorded.</summary>
    Task<VectorTileLayerRuntimeSettings?> GetStatusAsync(int layerId, CancellationToken cancellationToken);
}

/// <summary>Outcome of enqueuing a cache-version swap.</summary>
public sealed record CacheSwapResult(string JobId, string NewVersion);

/// <summary>Outcome of a synchronous cache invalidation.</summary>
public sealed record CacheInvalidateResult(int TilesInvalidated, int[] ZoomLevelsAffected);
