using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileOrchestrator : IVectorTileService
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<TileGeneration>>> InFlight = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> PendingRefresh = new();

    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileVariantResolver _variants;
    private readonly IVectorTileCache? _cache;
    private readonly IVectorTileEncoder _encoder;
    private readonly IVectorTileRuntimeSettingsStore _runtimeSettings;
    private readonly IServiceProvider _services;
    private readonly ILogger<VectorTileOrchestrator> _logger;
    private readonly ITileRefreshQueue? _refreshQueue;

    public VectorTileOrchestrator(
        IVectorTileLayerConfigProvider layers,
        IVectorTileVariantResolver variants,
        IVectorTileEncoder encoder,
        IVectorTileRuntimeSettingsStore runtimeSettings,
        IServiceProvider services,
        ILogger<VectorTileOrchestrator> logger,
        IVectorTileCache? cache = null,
        ITileRefreshQueue? refreshQueue = null)
    {
        _layers = layers;
        _variants = variants;
        _encoder = encoder;
        _runtimeSettings = runtimeSettings;
        _services = services;
        _logger = logger;
        _cache = cache;
        _refreshQueue = refreshQueue;
    }

    public async Task<VectorTileResult> GetTileAsync(int layerId, int z, int x, int y, string? variantKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var layer = _layers.GetLayer(layerId);
        if (layer is null || !layer.Enabled)
        {
            return VectorTileResult.Failure(VectorTileResultStatus.NotFound, "Layer not found");
        }

        if (!TileCoordinateUtils.IsValidTile(z, x, y))
        {
            return VectorTileResult.Failure(VectorTileResultStatus.BadRequest, "Invalid tile coordinates");
        }

        var context = CreateEncodingContext(layer, z, x, y);
        if (z < layer.Tile.MinZoom || z > layer.Tile.MaxZoom)
        {
            return layer.Tile.ReturnEmptyTileOutsideZoomRange
                ? Empty(layer, context)
                : VectorTileResult.Failure(VectorTileResultStatus.BadRequest, "Zoom is outside layer range");
        }

        // Outside the layer's configured data extent there is nothing to serve — short-circuit with
        // 204 before any cache lookup or database query.
        if (layer.Extent is { } extent &&
            !TileCoordinateUtils.ToMercatorEnvelope(extent).Intersects(context.TileEnvelope))
        {
            return VectorTileResult.NoContent();
        }

        var variant = _variants.Resolve(layer, variantKey);
        if (variant is null)
        {
            return VectorTileResult.Failure(VectorTileResultStatus.NotFound, "Variant not found");
        }

        var runtime = await GetOrCreateRuntimeSettings(layerId, cancellationToken);
        var key = new VectorTileCacheKey(layerId, z, x, y, variant.VariantKey, runtime.ActiveCacheVersion);

        if (_cache is not null)
        {
            var cached = await _cache.GetAsync(key, cancellationToken);
            if (cached is not null)
            {
                var stale = IsStale(layer, cached.WrittenAt);
                if (stale)
                {
                    TryEnqueueRefresh(layer, variant, z, x, y);
                }

                return new VectorTileResult { TileBytes = cached.Bytes, FromCache = true, IsStale = stale };
            }
        }

        if (!layer.Tile.AllowOnDemandGeneration)
        {
            return Empty(layer, context);
        }

        TileGeneration generated;
        try
        {
            // Single-flight: concurrent misses for the same tile generate once. Generation runs with
            // CancellationToken.None so one client aborting (e.g. panning the map) cannot cancel a tile
            // that other in-flight requests are still waiting on — each caller's own token only cancels
            // *their* await via WaitAsync, while generation completes and populates the cache.
            var generation = InFlight.GetOrAdd(key.ToStringKey(), _ => new Lazy<Task<TileGeneration>>(
                () => GenerateAsync(layer, variant, context, z, x, y, runtime, CancellationToken.None)));
            generated = await generation.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client aborted the request (routine with slippy-map pan/zoom). Not a server error —
            // log quietly and let ASP.NET Core treat it as an aborted request (no 503, no error log).
            _logger.LogDebug("VectorTileHub tile request canceled by client for layer {LayerId} tile {Z}/{X}/{Y}", layerId, z, x, y);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VectorTileHub tile generation failed for layer {LayerId} tile {Z}/{X}/{Y}", layerId, z, x, y);
            return VectorTileResult.Failure(VectorTileResultStatus.ServiceUnavailable, "Tile provider failed");
        }
        finally
        {
            InFlight.TryRemove(key.ToStringKey(), out _);
        }

        _logger.LogInformation(
            "Served vector tile {LayerId}/{Z}/{X}/{Y} variant {VariantKey} cache miss in {ElapsedMs}ms",
            layerId, z, x, y, variant.VariantKey, stopwatch.ElapsedMilliseconds);

        return new VectorTileResult { TileBytes = generated.Bytes, IsEmpty = generated.IsEmpty };
    }

    private readonly record struct TileGeneration(byte[] Bytes, bool IsEmpty);

    private async Task<TileGeneration> GenerateAsync(
        VectorTileLayerConfig layer,
        VectorTileVariant variant,
        VectorTileEncodingContext context,
        int z, int x, int y,
        VectorTileLayerRuntimeSettings runtime,
        CancellationToken cancellationToken)
    {
        var provider = _services.GetKeyedService<IVectorTileFeatureProvider>(layer.Provider.Type)
            ?? throw new InvalidOperationException($"No VectorTileHub provider registered for type '{layer.Provider.Type}'.");

        var envelope = TileCoordinateUtils.ExpandEnvelope(context.TileEnvelope, context.Buffer, context.Extent);
        var batch = await provider.GetFeaturesAsync(new VectorTileFeatureQuery
        {
            LayerConfig = layer,
            Envelope = envelope,
            Zoom = z,
            Variant = variant
        }, cancellationToken);

        var isEmpty = batch.Features.Count == 0;
        var bytes = isEmpty
            ? _encoder.EncodeEmpty(layer.LayerKey, context)
            : _encoder.Encode(layer.LayerKey, batch.Features, context);

        if (_cache is not null && layer.Cache.Enabled)
        {
            var key = new VectorTileCacheKey(layer.Id, z, x, y, variant.VariantKey, runtime.ActiveCacheVersion);
            await _cache.SetAsync(key, bytes, new VectorTileCacheOptions { CacheVersion = runtime.ActiveCacheVersion }, cancellationToken);
        }

        return new TileGeneration(bytes, isEmpty);
    }

    private static bool IsStale(VectorTileLayerConfig layer, DateTimeOffset writtenAt)
    {
        var period = layer.Cache.RefreshPeriodMinutes;
        return period > 0 && DateTimeOffset.UtcNow - writtenAt > TimeSpan.FromMinutes(period);
    }

    private void TryEnqueueRefresh(VectorTileLayerConfig layer, VectorTileVariant variant, int z, int x, int y)
    {
        if (_refreshQueue is null)
        {
            return;
        }

        var guardKey = $"{layer.Id}:{variant.VariantKey}:{z}:{x}:{y}";
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, layer.Cache.RefreshPeriodMinutes));

        if (PendingRefresh.TryGetValue(guardKey, out var last) && now - last < window)
        {
            return; // a refresh was enqueued recently — don't duplicate
        }

        PendingRefresh[guardKey] = now;
        try
        {
            _refreshQueue.EnqueueTileRefresh(layer.Id, variant.VariantKey, z, x, y);
        }
        catch (Exception ex)
        {
            PendingRefresh.TryRemove(guardKey, out _);
            _logger.LogWarning(ex, "Failed to enqueue stale-tile refresh for {GuardKey}", guardKey);
        }
    }

    private VectorTileResult Empty(VectorTileLayerConfig layer, VectorTileEncodingContext context)
    {
        return new VectorTileResult
        {
            TileBytes = _encoder.EncodeEmpty(layer.LayerKey, context),
            IsEmpty = true
        };
    }

    private static VectorTileEncodingContext CreateEncodingContext(VectorTileLayerConfig layer, int z, int x, int y)
    {
        return new VectorTileEncodingContext
        {
            LayerKey = layer.LayerKey,
            Extent = layer.Tile.Extent,
            Buffer = layer.Tile.Buffer,
            ClipGeometry = layer.Tile.ClipGeometry,
            TileEnvelope = TileCoordinateUtils.GetTileEnvelope(z, x, y)
        };
    }

    private async Task<VectorTileLayerRuntimeSettings> GetOrCreateRuntimeSettings(int layerId, CancellationToken cancellationToken)
    {
        var runtime = await _runtimeSettings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken);
        if (runtime is not null)
        {
            return runtime;
        }

        runtime = new VectorTileLayerRuntimeSettings
        {
            LayerId = layerId,
            ActiveCacheVersion = "default",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _runtimeSettings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
        return runtime;
    }
}
