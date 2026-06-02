using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class DiskTileCache : IVectorTileCache
{
    private readonly VectorTileHubOptions _options;
    private readonly ILogger<DiskTileCache> _logger;

    public DiskTileCache(IOptions<VectorTileHubOptions> options, ILogger<DiskTileCache> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CachedTile?> GetAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        var path = key.ToDiskPath(_options.DefaultCacheRootFolder);
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var writtenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        return new CachedTile(bytes, writtenAt);
    }

    public async Task SetAsync(VectorTileCacheKey key, byte[] tileBytes, VectorTileCacheOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var path = key.ToDiskPath(_options.DefaultCacheRootFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, tileBytes, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to write tile cache entry {CacheKey}", key.ToStringKey());
        }
    }

    public Task RemoveAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        try
        {
            var path = key.ToDiskPath(_options.DefaultCacheRootFolder);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to remove tile cache entry {CacheKey}", key.ToStringKey());
        }

        return Task.CompletedTask;
    }

    public async Task RemoveByEnvelopeAsync(int layerId, Envelope boundingBox, int minZoom, int maxZoom, string? variantKey, string cacheVersion, CancellationToken cancellationToken)
    {
        foreach (var (z, x, y) in TileCoordinateUtils.GetAffectedTilesForZoomRange(boundingBox, minZoom, maxZoom))
        {
            var key = new VectorTileCacheKey(layerId, z, x, y, variantKey ?? VectorTileVariant.DefaultKey, cacheVersion);
            await RemoveAsync(key, cancellationToken);
        }
    }
}
