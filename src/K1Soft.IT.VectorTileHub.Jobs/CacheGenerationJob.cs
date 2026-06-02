using System.Globalization;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Index.Strtree;

namespace K1Soft.IT.VectorTileHub.Jobs;

public sealed class CacheGenerationJob
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IVectorTileVariantResolver _variants;
    private readonly IVectorTileEncoder _encoder;
    private readonly IVectorTileCache _cache;
    private readonly IVectorTileRuntimeSettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<CacheGenerationJob> _logger;

    public CacheGenerationJob(
        IVectorTileLayerConfigProvider layers,
        IVectorTileVariantResolver variants,
        IVectorTileEncoder encoder,
        IVectorTileCache cache,
        IVectorTileRuntimeSettingsStore settings,
        IServiceProvider services,
        ILogger<CacheGenerationJob> logger)
    {
        _layers = layers;
        _variants = variants;
        _encoder = encoder;
        _cache = cache;
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public async Task Execute(int layerId, int? minZoom, int? maxZoom, string[]? variantKeys, int? maxDegreeOfParallelism, PerformContext? context, CancellationToken cancellationToken)
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

            var variants = ResolveVariants(layer, variantKeys);
            var zMin = minZoom ?? layer.Tile.MinZoom;
            var zMax = maxZoom ?? layer.Tile.MaxZoom;

            // Cap the generation zoom ceiling (serving zoom range is unaffected). High zooms dominate
            // the tile count, so this keeps full-layer generation tractable; zooms above the cap are
            // served on demand.
            if (layer.Tile.MaxGenerationZoom is { } generationCap)
            {
                zMax = Math.Min(zMax, generationCap);
            }

            // Bound generation to the configured layer extent. Without an extent we fall back to the
            // whole world, which is rarely practical — warn so the operator knows to configure one.
            NetTopologySuite.Geometries.Envelope bounds;
            if (layer.Extent is { } extent)
            {
                bounds = TileCoordinateUtils.ToMercatorEnvelope(extent);
            }
            else
            {
                bounds = new NetTopologySuite.Geometries.Envelope(-20037508.342789244, 20037508.342789244, -20037508.342789244, 20037508.342789244);
                _logger.LogWarning(
                    "Cache generation for layer {LayerId} has no configured extent; generating across the whole world for zoom {ZMin}-{ZMax}. Configure an 'extent' to bound generation.",
                    layerId, zMin, zMax);
            }

            // Tiles × variants is the total unit of work — used for the dashboard progress bar.
            var total = TileCoordinateUtils.CountAffectedTiles(bounds, zMin, zMax) * Math.Max(1, variants.Count);
            var dop = maxDegreeOfParallelism is > 0 ? maxDegreeOfParallelism.Value : Math.Clamp(Environment.ProcessorCount, 2, 8);

            var progressBar = context?.WriteProgressBar();
            context?.WriteLine($"Generating {total:N0} tiles for layer {layerId} (zoom {zMin}-{zMax}, {variants.Count} variant(s)) with {dop} parallel workers.");

            // When an extent bounds the data, load every feature ONCE into an in-memory R-tree and slice
            // each tile from memory — turning millions of per-tile spatial queries into a single query.
            // Without an extent we fall back to one (unfiltered) provider query per tile.
            STRtree<VectorTileFeature>? index = null;
            if (layer.Extent is not null)
            {
                context?.WriteLine("Loading features for the extent into memory...");
                var loaded = await provider.GetFeaturesAsync(new VectorTileFeatureQuery
                {
                    LayerConfig = layer,
                    Envelope = bounds,
                    Zoom = zMin,
                    Variant = Unfiltered
                }, cancellationToken);

                index = new STRtree<VectorTileFeature>();
                foreach (var feature in loaded.Features)
                {
                    index.Insert(feature.Geometry.EnvelopeInternal, feature);
                }

                index.Build(); // build on this thread so subsequent parallel queries are read-only & thread-safe
                context?.WriteLine($"Loaded {loaded.Features.Count:N0} features into the in-memory index; slicing tiles from memory.");
            }

            var processed = 0L;
            var lastReportMs = 0L;
            var progressSync = new object();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(
                TileCoordinateUtils.GetAffectedTilesForZoomRange(bounds, zMin, zMax),
                parallelOptions,
                async (tile, ct) =>
                {
                    var (z, x, y) = tile;
                    var tileEnvelope = TileCoordinateUtils.GetTileEnvelope(z, x, y);
                    var fetchEnvelope = TileCoordinateUtils.ExpandEnvelope(tileEnvelope, layer.Tile.Buffer, layer.Tile.Extent);
                    var tileContext = new VectorTileEncodingContext { LayerKey = layer.LayerKey, Extent = layer.Tile.Extent, Buffer = layer.Tile.Buffer, ClipGeometry = layer.Tile.ClipGeometry, TileEnvelope = tileEnvelope };

                    // Unfiltered candidate features for this tile, from the in-memory index or a per-tile query.
                    IReadOnlyList<VectorTileFeature> candidates = index is not null
                        ? index.Query(fetchEnvelope).ToList()
                        : (await provider.GetFeaturesAsync(new VectorTileFeatureQuery
                        {
                            LayerConfig = layer,
                            Envelope = fetchEnvelope,
                            Zoom = z,
                            Variant = Unfiltered
                        }, ct)).Features;

                    foreach (var variant in variants)
                    {
                        // Variant filters are applied in memory so the candidate set is fetched only once.
                        var features = variant.Filter is null
                            ? candidates
                            : candidates.Where(f => MatchesFilter(f, variant.Filter)).ToList();

                        var bytes = features.Count == 0
                            ? _encoder.EncodeEmpty(layer.LayerKey, tileContext)
                            : _encoder.Encode(layer.LayerKey, features, tileContext);
                        await _cache.SetAsync(new VectorTileCacheKey(layerId, z, x, y, variant.VariantKey, runtime.ActiveCacheVersion), bytes, new VectorTileCacheOptions { CacheVersion = runtime.ActiveCacheVersion }, ct);

                        var done = Interlocked.Increment(ref processed);
                        // Report on a time cadence (~every 2s) rather than per-percent — with millions of
                        // tiles a 1% step is huge, so a time-based report with rate + ETA shows real movement.
                        if (total > 0 && stopwatch.ElapsedMilliseconds - Interlocked.Read(ref lastReportMs) >= 2000)
                        {
                            lock (progressSync)
                            {
                                var nowMs = stopwatch.ElapsedMilliseconds;
                                if (nowMs - lastReportMs >= 2000)
                                {
                                    lastReportMs = nowMs;
                                    var pct = done * 100.0 / total;
                                    var rate = done / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                                    var eta = rate > 0 ? TimeSpan.FromSeconds((total - done) / rate) : TimeSpan.Zero;
                                    progressBar?.SetValue(pct);
                                    context?.WriteLine($"{done:N0}/{total:N0} ({pct:F1}%) — {rate:F0} tiles/s, ETA {eta:hh\\:mm\\:ss}");
                                }
                            }
                        }
                    }
                });

            progressBar?.SetValue(100);
            context?.WriteLine($"Completed: {processed:N0} tiles written in {stopwatch.Elapsed:hh\\:mm\\:ss}.");
            runtime.CacheGenerationStatus = CacheGenerationStatus.Idle;
            runtime.LastGenerationCompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // Mark failed; tiles already written remain usable and the job can be retried.
            runtime.CacheGenerationStatus = CacheGenerationStatus.Failed;
            context?.WriteLine($"Cache generation failed: {ex.Message}");
            _logger.LogError(ex, "VectorTileHub cache generation failed for layer {LayerId}", layerId);
        }

        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
    }

    // A no-filter variant used to load the full candidate set; per-variant filters are applied in memory.
    private static readonly VectorTileVariant Unfiltered = new() { VariantKey = VectorTileVariant.DefaultKey, IsDefault = true };

    /// <summary>
    /// In-memory equivalent of <see cref="VariantFilterSql"/> — evaluates a resolved variant filter
    /// against a feature's attributes so filtered variants can reuse one unfiltered candidate set.
    /// String comparison is case-insensitive to mirror typical SQL Server collation.
    /// </summary>
    private static bool MatchesFilter(VectorTileFeature feature, ResolvedFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Column))
        {
            return true;
        }

        feature.Attributes.TryGetValue(filter.Column, out var raw);
        var value = raw is null or DBNull ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);

        return filter.Operator switch
        {
            FilterOperator.IsNull => value is null,
            FilterOperator.IsNotNull => value is not null,
            FilterOperator.NotEquals => filter.Values.Length == 0 || (value is not null && !ValueEquals(value, filter.Values[0])),
            FilterOperator.Equals or FilterOperator.In => filter.Values.Length == 0 || (value is not null && filter.Values.Any(v => ValueEquals(value, v))),
            _ => true
        };
    }

    private static bool ValueEquals(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<VectorTileVariant> ResolveVariants(VectorTileLayerConfig layer, string[]? variantKeys)
    {
        if (variantKeys is { Length: > 0 })
        {
            return variantKeys
                .Select(k => _variants.Resolve(layer, k))
                .Where(v => v is not null)
                .Select(v => v!)
                .ToArray();
        }

        if (layer.CacheRules.Count == 0)
        {
            var d = _variants.Resolve(layer, null);
            return d is null ? [] : [d];
        }

        return layer.CacheRules
            .Select(r => _variants.Resolve(layer, r.VariantKey))
            .Where(v => v is not null)
            .Select(v => v!)
            .ToArray();
    }
}
