using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileFeature
{
    public object Id { get; init; } = "";
    public Geometry Geometry { get; init; } = GeometryFactory.Default.CreateGeometryCollection();
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();
}
