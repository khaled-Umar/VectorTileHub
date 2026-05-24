namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileFeatureBatch
{
    public IReadOnlyList<VectorTileFeature> Features { get; init; } = [];
    public int TotalCount { get; init; }
}
