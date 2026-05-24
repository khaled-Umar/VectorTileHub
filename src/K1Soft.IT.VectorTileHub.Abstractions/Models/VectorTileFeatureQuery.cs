using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileFeatureQuery
{
    public VectorTileLayerConfig LayerConfig { get; init; } = new();
    public Envelope Envelope { get; init; } = new();
    public int Zoom { get; init; }
    public VectorTileSecurityScope SecurityScope { get; init; } = new();
}
