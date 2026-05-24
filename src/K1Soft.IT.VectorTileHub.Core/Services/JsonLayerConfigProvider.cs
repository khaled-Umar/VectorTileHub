using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub;

public sealed class JsonLayerConfigProvider : IVectorTileLayerConfigProvider
{
    private readonly VectorTileHubOptions _options;
    private readonly ILogger<JsonLayerConfigProvider> _logger;
    private ConcurrentDictionary<int, VectorTileLayerConfig> _layers = new();

    public JsonLayerConfigProvider(IOptions<VectorTileHubOptions> options, ILogger<JsonLayerConfigProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _layers = LoadLayers();
    }

    public VectorTileLayerConfig? GetLayer(int layerId) => _layers.TryGetValue(layerId, out var layer) ? layer : null;

    public VectorTileLayerConfig? GetLayerByKey(string layerKey)
    {
        return _layers.Values.FirstOrDefault(x => string.Equals(x.LayerKey, layerKey, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<VectorTileLayerConfig> GetAllLayers() => _layers.Values.OrderBy(x => x.Id).ToArray();

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        var reloaded = LoadLayers();
        Interlocked.Exchange(ref _layers, reloaded);
        return Task.CompletedTask;
    }

    private ConcurrentDictionary<int, VectorTileLayerConfig> LoadLayers()
    {
        var result = new ConcurrentDictionary<int, VectorTileLayerConfig>();
        var folder = Path.GetFullPath(_options.LayerConfigFolder);

        if (!Directory.Exists(folder))
        {
            _logger.LogWarning("VectorTileHub layer config folder does not exist: {Folder}", folder);
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var layer = JsonSerializer.Deserialize<VectorTileLayerConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (layer is null || layer.Id <= 0)
                {
                    _logger.LogWarning("Skipping invalid VectorTileHub layer config: {File}", file);
                    continue;
                }

                ApplyDefaults(layer);
                result[layer.Id] = layer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid VectorTileHub layer config: {File}", file);
            }
        }

        return result;
    }

    private void ApplyDefaults(VectorTileLayerConfig layer)
    {
        layer.Tile.Extent = layer.Tile.Extent == 0 ? _options.DefaultTileExtent : layer.Tile.Extent;
        layer.Tile.Buffer = layer.Tile.Buffer == 0 ? _options.DefaultTileBuffer : layer.Tile.Buffer;
        layer.Tile.ServingSrid = layer.Tile.ServingSrid == 0 ? _options.DefaultServingSrid : layer.Tile.ServingSrid;
        layer.Cache.CacheRootFolder ??= _options.DefaultCacheRootFolder;
    }
}
