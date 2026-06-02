using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K1Soft.IT.VectorTileHub.Jobs;

/// <summary>
/// Regenerates a single stale tile in the background (used by stale-while-revalidate).
/// </summary>
public sealed class CacheTileRefreshJob
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileVariantResolver _variants;
    private readonly IVectorTileEncoder _encoder;
    private readonly IVectorTileCache _cache;
    private readonly IVectorTileRuntimeSettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<CacheTileRefreshJob> _logger;

    public CacheTileRefreshJob(
        IVectorTileLayerConfigProvider layers,
        IVectorTileVariantResolver variants,
        IVectorTileEncoder encoder,
        IVectorTileCache cache,
        IVectorTileRuntimeSettingsStore settings,
        IServiceProvider services,
        ILogger<CacheTileRefreshJob> logger)
    {
        _layers = layers;
        _variants = variants;
        _encoder = encoder;
        _cache = cache;
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public async Task Execute(int layerId, string variantKey, int z, int x, int y, CancellationToken cancellationToken)
    {
        var layer = _layers.GetLayer(layerId);
        if (layer is null)
        {
            return;
        }

        var variant = _variants.Resolve(layer, variantKey);
        if (variant is null)
        {
            return;
        }

        var provider = _services.GetKeyedService<IVectorTileFeatureProvider>(layer.Provider.Type);
        if (provider is null)
        {
            _logger.LogWarning("Refresh skipped: provider {ProviderType} not registered", layer.Provider.Type);
            return;
        }

        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
        var envelope = TileCoordinateUtils.GetTileEnvelope(z, x, y);
        var context = new VectorTileEncodingContext { LayerKey = layer.LayerKey, Extent = layer.Tile.Extent, Buffer = layer.Tile.Buffer, ClipGeometry = layer.Tile.ClipGeometry, TileEnvelope = envelope };

        var batch = await provider.GetFeaturesAsync(new VectorTileFeatureQuery
        {
            LayerConfig = layer,
            Envelope = TileCoordinateUtils.ExpandEnvelope(envelope, layer.Tile.Buffer, layer.Tile.Extent),
            Zoom = z,
            Variant = variant
        }, cancellationToken);

        var bytes = batch.Features.Count == 0 ? _encoder.EncodeEmpty(layer.LayerKey, context) : _encoder.Encode(layer.LayerKey, batch.Features, context);
        await _cache.SetAsync(new VectorTileCacheKey(layerId, z, x, y, variant.VariantKey, runtime.ActiveCacheVersion), bytes, new VectorTileCacheOptions { CacheVersion = runtime.ActiveCacheVersion }, cancellationToken);
    }
}
