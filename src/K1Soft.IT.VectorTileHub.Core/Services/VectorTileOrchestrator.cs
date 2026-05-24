using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileOrchestrator : IVectorTileService
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileSecurityScopeResolver _scopes;
    private readonly IVectorTileCache? _cache;
    private readonly IVectorTileEncoder _encoder;
    private readonly IVectorTileRuntimeSettingsStore _runtimeSettings;
    private readonly IServiceProvider _services;
    private readonly ILogger<VectorTileOrchestrator> _logger;

    public VectorTileOrchestrator(
        IVectorTileLayerConfigProvider layers,
        IVectorTileSecurityScopeResolver scopes,
        IVectorTileEncoder encoder,
        IVectorTileRuntimeSettingsStore runtimeSettings,
        IServiceProvider services,
        ILogger<VectorTileOrchestrator> logger,
        IVectorTileCache? cache = null)
    {
        _layers = layers;
        _scopes = scopes;
        _encoder = encoder;
        _runtimeSettings = runtimeSettings;
        _services = services;
        _logger = logger;
        _cache = cache;
    }

    public async Task<VectorTileResult> GetTileAsync(int layerId, int z, int x, int y, ClaimsPrincipal user, string? scopeOverride, CancellationToken cancellationToken)
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
            if (layer.Tile.ReturnEmptyTileOutsideZoomRange)
            {
                return Empty(layer, context);
            }

            return VectorTileResult.Failure(VectorTileResultStatus.BadRequest, "Zoom is outside layer range");
        }

        var scope = await _scopes.ResolveAsync(layer, user, scopeOverride, cancellationToken);
        if (!scope.IsAuthenticated && (layer.Security?.RequireAuthentication ?? true))
        {
            return VectorTileResult.Failure(VectorTileResultStatus.Unauthorized, "Authentication required");
        }

        if (!scope.IsAuthorized)
        {
            return VectorTileResult.Failure(VectorTileResultStatus.Forbidden, "Scope is not authorized");
        }

        var runtime = await GetOrCreateRuntimeSettings(layerId, cancellationToken);
        var key = new VectorTileCacheKey(layerId, z, x, y, scope.ScopeKey, runtime.ActiveCacheVersion);
        if (_cache is not null)
        {
            var cached = await _cache.GetAsync(key, cancellationToken);
            if (cached is not null)
            {
                return new VectorTileResult { TileBytes = cached, FromCache = true };
            }
        }

        if (!layer.Tile.AllowOnDemandGeneration)
        {
            return Empty(layer, context);
        }

        var provider = _services.GetKeyedService<IVectorTileFeatureProvider>(layer.Provider.Type);
        if (provider is null)
        {
            _logger.LogError("No VectorTileHub provider registered for type {ProviderType}", layer.Provider.Type);
            return VectorTileResult.Failure(VectorTileResultStatus.ServiceUnavailable, "Provider is not registered");
        }

        var envelope = TileCoordinateUtils.ExpandEnvelope(context.TileEnvelope, context.Buffer, context.Extent);
        VectorTileFeatureBatch batch;
        byte[] bytes;
        try
        {
            batch = await provider.GetFeaturesAsync(new VectorTileFeatureQuery
            {
                LayerConfig = layer,
                Envelope = envelope,
                Zoom = z,
                SecurityScope = scope
            }, cancellationToken);

            bytes = batch.Features.Count == 0
                ? _encoder.EncodeEmpty(layer.LayerKey, context)
                : _encoder.Encode(layer.LayerKey, batch.Features, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VectorTileHub provider failed for layer {LayerId} tile {Z}/{X}/{Y}", layerId, z, x, y);
            return VectorTileResult.Failure(VectorTileResultStatus.ServiceUnavailable, "Tile provider failed");
        }

        if (_cache is not null && layer.Cache.Enabled)
        {
            await _cache.SetAsync(key, bytes, new VectorTileCacheOptions
            {
                CacheVersion = runtime.ActiveCacheVersion,
                TtlMinutes = layer.Cache.TtlMinutes
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Served vector tile {LayerId}/{Z}/{X}/{Y} scope {ScopeKey} cache miss in {ElapsedMs}ms",
            layerId,
            z,
            x,
            y,
            scope.ScopeKey,
            stopwatch.ElapsedMilliseconds);

        return new VectorTileResult { TileBytes = bytes, IsEmpty = batch.Features.Count == 0 };
    }

    private static VectorTileResult Empty(VectorTileLayerConfig layer, VectorTileEncodingContext context)
    {
        var encoder = new MapboxVectorTileEncoder();
        return new VectorTileResult
        {
            TileBytes = encoder.EncodeEmpty(layer.LayerKey, context),
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
