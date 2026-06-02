namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// A cached tile plus the timestamp it was written. <see cref="WrittenAt"/> drives
/// stale-while-revalidate: a tile older than the layer's refresh period is served
/// immediately while a background refresh is enqueued.
/// </summary>
public sealed record CachedTile(byte[] Bytes, DateTimeOffset WrittenAt);
