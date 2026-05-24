namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileCacheOptions
{
    public int TtlMinutes { get; init; }
    public string CacheVersion { get; init; } = "default";
}
