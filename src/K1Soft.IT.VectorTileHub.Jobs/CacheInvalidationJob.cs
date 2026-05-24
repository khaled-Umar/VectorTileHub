using NetTopologySuite.Geometries;

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

    public async Task Execute(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? scopes, CancellationToken cancellationToken)
    {
        var layer = _layers.GetLayer(layerId) ?? throw new InvalidOperationException($"Layer {layerId} not found.");
        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
        var envelope = new Envelope(minX, maxX, minY, maxY);
        var scopeList = scopes is { Length: > 0 } ? scopes : ["public"];
        foreach (var scope in scopeList)
        {
            await _cache.RemoveByEnvelopeAsync(layerId, envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom, scope, runtime.ActiveCacheVersion, cancellationToken);
        }

        runtime.LastInvalidatedAt = DateTimeOffset.UtcNow;
        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
    }
}
