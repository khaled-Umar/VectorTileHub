using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class MemoryTileCache : IVectorTileCache
{
    private readonly IMemoryCache _cache;

    public MemoryTileCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<CachedTile?> GetAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_cache.TryGetValue<CachedTile>(key.ToStringKey(), out var value) ? value : null);
    }

    public Task SetAsync(VectorTileCacheKey key, byte[] tileBytes, VectorTileCacheOptions options, CancellationToken cancellationToken)
    {
        // Stale-while-revalidate keeps stale tiles available, so memory entries are not
        // evicted by the layer refresh period; staleness is computed from WrittenAt.
        _cache.Set(key.ToStringKey(), new CachedTile(tileBytes, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        _cache.Remove(key.ToStringKey());
        return Task.CompletedTask;
    }

    public Task RemoveByEnvelopeAsync(int layerId, Envelope boundingBox, int minZoom, int maxZoom, string? variantKey, string cacheVersion, CancellationToken cancellationToken)
    {
        foreach (var (z, x, y) in TileCoordinateUtils.GetAffectedTilesForZoomRange(boundingBox, minZoom, maxZoom))
        {
            _cache.Remove(new VectorTileCacheKey(layerId, z, x, y, variantKey ?? VectorTileVariant.DefaultKey, cacheVersion).ToStringKey());
        }

        return Task.CompletedTask;
    }
}
