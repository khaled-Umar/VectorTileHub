using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileFeatureQuery
{
    public VectorTileLayerConfig LayerConfig { get; init; } = new();
    public Envelope Envelope { get; init; } = new();
    public int Zoom { get; init; }

    /// <summary>The resolved variant (key + optional server-side filter) for this request.</summary>
    public VectorTileVariant Variant { get; init; } = new();
}
