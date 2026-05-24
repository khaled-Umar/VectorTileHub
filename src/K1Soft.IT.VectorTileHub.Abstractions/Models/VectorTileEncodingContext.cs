using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileEncodingContext
{
    public string LayerKey { get; init; } = "";
    public int Extent { get; init; } = 4096;
    public int Buffer { get; init; } = 64;
    public bool ClipGeometry { get; init; } = true;
    public Envelope TileEnvelope { get; init; } = new();
}
