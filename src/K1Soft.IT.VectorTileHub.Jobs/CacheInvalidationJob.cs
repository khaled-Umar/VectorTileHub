namespace K1Soft.IT.VectorTileHub.Jobs;

public sealed class CacheInvalidationJob
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileCache _cache;
    private readonly IVectorTileRuntimeSettingsStore _settings;

    public CacheInvalidationJob(IVectorTileLayerConfigProvider layers, IVectorTileCache cache, IVectorTileRuntimeSettingsStore settings)
    {
        _layers = layers;
        _cache = cache;
        _settings = settings;
    }

    public async Task Execute(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? variantKeys, CancellationToken cancellationToken)
    {
        var layer = _layers.GetLayer(layerId) ?? throw new InvalidOperationException($"Layer {layerId} not found.");
        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };

        // Normalise the caller's bbox (4326 or 3857) to Web Mercator metres before tile math.
        var envelope = TileCoordinateUtils.ToMercatorEnvelope(minX, minY, maxX, maxY, srid);

        var keys = variantKeys is { Length: > 0 }
            ? variantKeys
            : layer.CacheRules.Count > 0 ? layer.CacheRules.Select(r => r.VariantKey).ToArray() : [VectorTileVariant.DefaultKey];

        foreach (var variantKey in keys)
        {
            await _cache.RemoveByEnvelopeAsync(layerId, envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom, variantKey, runtime.ActiveCacheVersion, cancellationToken);
        }

        runtime.LastInvalidatedAt = DateTimeOffset.UtcNow;
        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
    }
}
