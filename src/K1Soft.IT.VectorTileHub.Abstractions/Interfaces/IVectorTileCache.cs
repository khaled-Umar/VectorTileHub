using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileCache
{
    Task<CachedTile?> GetAsync(
        VectorTileCacheKey key,
        CancellationToken cancellationToken);

    Task SetAsync(
        VectorTileCacheKey key,
        byte[] tileBytes,
        VectorTileCacheOptions options,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        VectorTileCacheKey key,
        CancellationToken cancellationToken);

    Task RemoveByEnvelopeAsync(
        int layerId,
        Envelope boundingBox,
        int minZoom,
        int maxZoom,
        string? variantKey,
        string cacheVersion,
        CancellationToken cancellationToken);
}
