using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Reprojects geometries and envelopes between coordinate reference systems so a feature provider can read
/// data stored in any source CRS and serve it in the tiling CRS (Web Mercator). Implementations resolve a
/// CRS from its SRID for the built-in systems (4326 / 3857) or from an explicit WKT definition for custom
/// projections (see <see cref="CoordinateReferenceSystem.Wkt"/>).
/// </summary>
public interface ICoordinateReprojector
{
    /// <summary>
    /// Reprojects <paramref name="geometry"/> from <paramref name="source"/> to <paramref name="target"/>,
    /// returning a copy whose <see cref="Geometry.SRID"/> is the target SRID. When the two CRSs are equal the
    /// geometry is copied through unchanged (only the SRID is set).
    /// </summary>
    Geometry Reproject(Geometry geometry, CoordinateReferenceSystem source, CoordinateReferenceSystem target);

    /// <summary>
    /// Reprojects the bounding box <paramref name="envelope"/> from <paramref name="source"/> to
    /// <paramref name="target"/>. The edges are densified before transforming so the resulting box fully
    /// covers the input even under non-affine (curved/rotated) projections — it never under-covers, so a
    /// spatial filter built from it cannot drop features near the tile edges.
    /// </summary>
    Envelope Reproject(Envelope envelope, CoordinateReferenceSystem source, CoordinateReferenceSystem target);
}
