using Hangfire;

namespace K1Soft.IT.VectorTileHub.Jobs;

/// <summary>
/// Hangfire-backed implementation of <see cref="IVectorTileCacheAdmin"/>. Holds the cache-admin logic
/// that previously lived in the library's AdminCacheController, so a host can call it from its own
/// (authorized) controllers instead of the library exposing endpoints.
/// </summary>
public sealed class HangfireVectorTileCacheAdmin : IVectorTileCacheAdmin
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileCache _cache;
    private readonly IVectorTileRuntimeSettingsStore _runtimeSettings;

    public HangfireVectorTileCacheAdmin(
        IBackgroundJobClient jobs,
        IVectorTileLayerConfigProvider layers,
        IVectorTileCache cache,
        IVectorTileRuntimeSettingsStore runtimeSettings)
    {
        _jobs = jobs;
        _layers = layers;
        _cache = cache;
        _runtimeSettings = runtimeSettings;
    }

    public string EnqueueGenerate(int layerId, int? minZoom, int? maxZoom, string[]? variants, int? maxDegreeOfParallelism) =>
        _jobs.Enqueue<CacheGenerationJob>(job => job.Execute(layerId, minZoom, maxZoom, variants, maxDegreeOfParallelism, null, CancellationToken.None));

    public string EnqueueDelete(int layerId, string? cacheVersion, bool deleteAllVersions) =>
        _jobs.Enqueue<CacheDeletionJob>(job => job.Execute(layerId, cacheVersion, deleteAllVersions, CancellationToken.None));

    public string EnqueueNotifyChange(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? variants) =>
        _jobs.Enqueue<CacheInvalidationJob>(job => job.Execute(layerId, minX, minY, maxX, maxY, srid, variants, CancellationToken.None));

    public CacheSwapResult EnqueueSwap(int layerId, string? newVersion, bool regenerateAfterSwap, bool deleteOldVersion)
    {
        var version = string.IsNullOrWhiteSpace(newVersion) ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") : newVersion;
        var jobId = _jobs.Enqueue<CacheSwapJob>(job => job.Execute(layerId, version, regenerateAfterSwap, deleteOldVersion, CancellationToken.None));
        return new CacheSwapResult(jobId, version);
    }

    public async Task<CacheInvalidateResult?> InvalidateAsync(
        int layerId,
        double minX,
        double minY,
        double maxX,
        double maxY,
        int srid,
        string[]? variants,
        CancellationToken cancellationToken)
    {
        var layer = _layers.GetLayer(layerId);
        if (layer is null)
        {
            return null;
        }

        var runtime = await _runtimeSettings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken)
            ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };

        // Tile math runs in Web Mercator metres, so normalise the caller's bbox (which may be in 4326
        // or 3857) to 3857 first — otherwise a lon/lat bbox would target tiles at the map origin.
        var envelope = TileCoordinateUtils.ToMercatorEnvelope(minX, minY, maxX, maxY, srid);
        var tiles = TileCoordinateUtils.GetAffectedTilesForZoomRange(envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom).ToArray();
        var resolvedVariants = variants is { Length: > 0 }
            ? variants
            : layer.CacheRules.Count > 0 ? layer.CacheRules.Select(r => r.VariantKey).ToArray() : [VectorTileVariant.DefaultKey];

        foreach (var variant in resolvedVariants)
        {
            await _cache.RemoveByEnvelopeAsync(layerId, envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom, variant, runtime.ActiveCacheVersion, cancellationToken);
        }

        runtime.LastInvalidatedAt = DateTimeOffset.UtcNow;
        await _runtimeSettings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);

        return new CacheInvalidateResult(
            tiles.Length * resolvedVariants.Length,
            tiles.Select(x => x.z).Distinct().Order().ToArray());
    }

    public Task<VectorTileLayerRuntimeSettings?> GetStatusAsync(int layerId, CancellationToken cancellationToken) =>
        _runtimeSettings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken);
}
