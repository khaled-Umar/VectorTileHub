using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Result of a configuration (re)load: which layers loaded and which files failed.
/// </summary>
public sealed record LayerReloadResult(
    IReadOnlyList<int> Loaded,
    IReadOnlyList<LayerLoadError> Failed);

public sealed record LayerLoadError(string Path, string Error);

public sealed class JsonLayerConfigProvider : IVectorTileLayerConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VectorTileHubOptions _options;
    private readonly ILogger<JsonLayerConfigProvider> _logger;
    private ConcurrentDictionary<int, VectorTileLayerConfig> _layers = new();
    private LayerReloadResult _lastResult = new([], []);

    public JsonLayerConfigProvider(IOptions<VectorTileHubOptions> options, ILogger<JsonLayerConfigProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _layers = LoadLayers(out _lastResult);
    }

    public VectorTileLayerConfig? GetLayer(int layerId) => _layers.TryGetValue(layerId, out var layer) ? layer : null;

    public VectorTileLayerConfig? GetLayerByKey(string layerKey)
    {
        return _layers.Values.FirstOrDefault(x => string.Equals(x.LayerKey, layerKey, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<VectorTileLayerConfig> GetAllLayers() => _layers.Values.OrderBy(x => x.Id).ToArray();

    public LayerReloadResult LastResult => _lastResult;

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        var reloaded = LoadLayers(out var result);
        Interlocked.Exchange(ref _layers, reloaded);
        _lastResult = result;
        return Task.CompletedTask;
    }

    private ConcurrentDictionary<int, VectorTileLayerConfig> LoadLayers(out LayerReloadResult result)
    {
        var loaded = new ConcurrentDictionary<int, VectorTileLayerConfig>();
        var errors = new List<LayerLoadError>();

        foreach (var file in EnumerateConfigFiles())
        {
            try
            {
                var json = File.ReadAllText(file);
                var layer = JsonSerializer.Deserialize<VectorTileLayerConfig>(json, JsonOptions);

                var validationError = Validate(layer);
                if (validationError is not null)
                {
                    errors.Add(new LayerLoadError(file, validationError));
                    _logger.LogWarning("VectorTileHub layer config rejected ({File}): {Error}", file, validationError);
                    continue;
                }

                if (loaded.ContainsKey(layer!.Id))
                {
                    var dup = $"Duplicate layer id {layer.Id}";
                    errors.Add(new LayerLoadError(file, dup));
                    _logger.LogWarning("VectorTileHub layer config rejected ({File}): {Error}", file, dup);
                    continue;
                }

                ApplyDefaults(layer);
                loaded[layer.Id] = layer;
            }
            catch (Exception ex)
            {
                errors.Add(new LayerLoadError(file, ex.Message));
                _logger.LogWarning(ex, "Failed to load VectorTileHub layer config: {File}", file);
            }
        }

        result = new LayerReloadResult(loaded.Keys.OrderBy(x => x).ToArray(), errors);
        return loaded;
    }

    private IEnumerable<string> EnumerateConfigFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _options.LayerConfigPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var full = Path.GetFullPath(path);
            if (File.Exists(full) && seen.Add(full))
            {
                yield return full;
            }
            else if (!File.Exists(full))
            {
                _logger.LogWarning("VectorTileHub layer config path not found: {Path}", full);
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.LayerConfigFolder))
        {
            var folder = Path.GetFullPath(_options.LayerConfigFolder);
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
                {
                    var full = Path.GetFullPath(file);
                    if (seen.Add(full))
                    {
                        yield return full;
                    }
                }
            }
        }
    }

    private static string? Validate(VectorTileLayerConfig? layer)
    {
        if (layer is null)
        {
            return "Config did not deserialize to a layer.";
        }

        if (layer.Id <= 0)
        {
            return "Missing or invalid 'Id' (must be a positive integer).";
        }

        if (string.IsNullOrWhiteSpace(layer.LayerKey))
        {
            return "Missing 'LayerKey'.";
        }

        if (string.IsNullOrWhiteSpace(layer.Provider.Type))
        {
            return "Missing 'Provider.Type'.";
        }

        if (string.IsNullOrWhiteSpace(layer.Provider.ConnectionString) &&
            string.IsNullOrWhiteSpace(layer.Provider.ConnectionStringName))
        {
            return "Missing 'Provider.ConnectionString' or 'Provider.ConnectionStringName'.";
        }

        if (string.IsNullOrWhiteSpace(layer.Provider.TableName))
        {
            return "Missing 'Provider.TableName'.";
        }

        if (string.IsNullOrWhiteSpace(layer.Provider.GeometryColumn))
        {
            return "Missing 'Provider.GeometryColumn'.";
        }

        if (layer.CacheRules.Count(r => r.IsDefault) > 1)
        {
            return "At most one cache rule may be marked IsDefault.";
        }

        return null;
    }

    private void ApplyDefaults(VectorTileLayerConfig layer)
    {
        layer.Tile.Extent = layer.Tile.Extent == 0 ? _options.DefaultTileExtent : layer.Tile.Extent;
        layer.Tile.Buffer = layer.Tile.Buffer == 0 ? _options.DefaultTileBuffer : layer.Tile.Buffer;
        layer.Tile.ServingSrid = layer.Tile.ServingSrid == 0 ? _options.DefaultServingSrid : layer.Tile.ServingSrid;
        layer.Cache.CacheRootFolder ??= _options.DefaultCacheRootFolder;

        // Guarantee a default variant exists when none is declared.
        if (layer.CacheRules.Count > 0 && !layer.CacheRules.Any(r => r.IsDefault))
        {
            layer.CacheRules[0].IsDefault = true;
        }
    }
}
