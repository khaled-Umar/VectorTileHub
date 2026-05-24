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

    public Task<byte[]?> GetAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_cache.TryGetValue<byte[]>(key.ToStringKey(), out var value) ? value : null);
    }

    public Task SetAsync(VectorTileCacheKey key, byte[] tileBytes, VectorTileCacheOptions options, CancellationToken cancellationToken)
    {
        var entryOptions = new MemoryCacheEntryOptions();
        if (options.TtlMinutes > 0)
        {
            entryOptions.SetSlidingExpiration(TimeSpan.FromMinutes(options.TtlMinutes));
        }

        _cache.Set(key.ToStringKey(), tileBytes, entryOptions);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        _cache.Remove(key.ToStringKey());
        return Task.CompletedTask;
    }

    public Task RemoveByEnvelopeAsync(int layerId, Envelope boundingBox, int minZoom, int maxZoom, string? scopeKey, string cacheVersion, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
