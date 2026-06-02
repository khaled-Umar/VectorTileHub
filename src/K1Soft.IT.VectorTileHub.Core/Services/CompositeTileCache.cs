using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class CompositeTileCache : IVectorTileCache
{
    private readonly MemoryTileCache? _memory;
    private readonly DiskTileCache? _disk;

    public CompositeTileCache(MemoryTileCache? memory, DiskTileCache? disk)
    {
        _memory = memory;
        _disk = disk;
    }

    public async Task<CachedTile?> GetAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        if (_memory is not null)
        {
            var memoryValue = await _memory.GetAsync(key, cancellationToken);
            if (memoryValue is not null)
            {
                return memoryValue;
            }
        }

        if (_disk is null)
        {
            return null;
        }

        var diskValue = await _disk.GetAsync(key, cancellationToken);
        if (diskValue is not null && _memory is not null)
        {
            await _memory.SetAsync(key, diskValue.Bytes, new VectorTileCacheOptions { CacheVersion = key.CacheVersion }, cancellationToken);
        }

        return diskValue;
    }

    public async Task SetAsync(VectorTileCacheKey key, byte[] tileBytes, VectorTileCacheOptions options, CancellationToken cancellationToken)
    {
        if (_memory is not null)
        {
            await _memory.SetAsync(key, tileBytes, options, cancellationToken);
        }

        if (_disk is not null)
        {
            await _disk.SetAsync(key, tileBytes, options, cancellationToken);
        }
    }

    public async Task RemoveAsync(VectorTileCacheKey key, CancellationToken cancellationToken)
    {
        if (_memory is not null)
        {
            await _memory.RemoveAsync(key, cancellationToken);
        }

        if (_disk is not null)
        {
            await _disk.RemoveAsync(key, cancellationToken);
        }
    }

    public async Task RemoveByEnvelopeAsync(int layerId, Envelope boundingBox, int minZoom, int maxZoom, string? variantKey, string cacheVersion, CancellationToken cancellationToken)
    {
        if (_memory is not null)
        {
            await _memory.RemoveByEnvelopeAsync(layerId, boundingBox, minZoom, maxZoom, variantKey, cacheVersion, cancellationToken);
        }

        if (_disk is not null)
        {
            await _disk.RemoveByEnvelopeAsync(layerId, boundingBox, minZoom, maxZoom, variantKey, cacheVersion, cancellationToken);
        }
    }
}
