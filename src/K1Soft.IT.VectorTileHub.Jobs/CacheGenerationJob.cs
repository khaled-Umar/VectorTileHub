using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K1Soft.IT.VectorTileHub.Jobs;

public sealed class CacheGenerationJob
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileEncoder _encoder;
    private readonly IVectorTileCache _cache;
    private readonly IVectorTileRuntimeSettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<CacheGenerationJob> _logger;

    public CacheGenerationJob(IVectorTileLayerConfigProvider layers, IVectorTileEncoder encoder, IVectorTileCache cache, IVectorTileRuntimeSettingsStore settings, IServiceProvider services, ILogger<CacheGenerationJob> logger)
    {
        _layers = layers;
        _encoder = encoder;
        _cache = cache;
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public async Task Execute(int layerId, int? minZoom, int? maxZoom, string[]? scopes, CancellationToken cancellationToken)
    {
        var layer = _layers.GetLayer(layerId) ?? throw new InvalidOperationException($"Layer {layerId} not found.");
        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
        runtime.CacheGenerationStatus = CacheGenerationStatus.Running;
        runtime.LastGenerationStartedAt = DateTimeOffset.UtcNow;
        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);

        try
        {
            var provider = _services.GetKeyedService<IVectorTileFeatureProvider>(layer.Provider.Type)
                ?? throw new InvalidOperationException($"Provider {layer.Provider.Type} is not registered.");
            var scopeList = scopes is { Length: > 0 } ? scopes : ["public"];
            var zMin = minZoom ?? layer.Tile.MinZoom;
            var zMax = maxZoom ?? layer.Tile.MaxZoom;
            var world = new NetTopologySuite.Geometries.Envelope(-20037508.342789244, 20037508.342789244, -20037508.342789244, 20037508.342789244);

            foreach (var (z, x, y) in TileCoordinateUtils.GetAffectedTilesForZoomRange(world, zMin, zMax))
            {
                foreach (var scope in scopeList)
                {
                    var envelope = TileCoordinateUtils.GetTileEnvelope(z, x, y);
                    var context = new VectorTileEncodingContext { LayerKey = layer.LayerKey, Extent = layer.Tile.Extent, Buffer = layer.Tile.Buffer, ClipGeometry = layer.Tile.ClipGeometry, TileEnvelope = envelope };
                    var batch = await provider.GetFeaturesAsync(new VectorTileFeatureQuery
                    {
                        LayerConfig = layer,
                        Envelope = TileCoordinateUtils.ExpandEnvelope(envelope, layer.Tile.Buffer, layer.Tile.Extent),
                        Zoom = z,
                        SecurityScope = new VectorTileSecurityScope { ScopeKey = scope, FilterValues = [scope] }
                    }, cancellationToken);
                    var bytes = batch.Features.Count == 0 ? _encoder.EncodeEmpty(layer.LayerKey, context) : _encoder.Encode(layer.LayerKey, batch.Features, context);
                    await _cache.SetAsync(new VectorTileCacheKey(layerId, z, x, y, scope, runtime.ActiveCacheVersion), bytes, new VectorTileCacheOptions { CacheVersion = runtime.ActiveCacheVersion, TtlMinutes = layer.Cache.TtlMinutes }, cancellationToken);
                }
            }

            runtime.CacheGenerationStatus = CacheGenerationStatus.Idle;
            runtime.LastGenerationCompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            runtime.CacheGenerationStatus = CacheGenerationStatus.Failed;
            _logger.LogError(ex, "VectorTileHub cache generation failed for layer {LayerId}", layerId);
        }

        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
    }
}
