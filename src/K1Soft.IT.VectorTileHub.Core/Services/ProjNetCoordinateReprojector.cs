using System.Collections.Concurrent;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// <see cref="ICoordinateReprojector"/> backed by the managed PROJ engine (ProjNET) — no native dependency.
/// Built transforms are cached per source/target pair (keyed by SRID + WKT) and reused; ProjNET
/// <see cref="MathTransform"/> instances are stateless for point transforms, so the cache is safe to share.
/// Built-in systems (4326 / 3857) are resolved without WKT; any other SRID must carry a
/// <see cref="CoordinateReferenceSystem.Wkt"/> definition.
/// </summary>
public sealed class ProjNetCoordinateReprojector : ICoordinateReprojector
{
    /// <summary>Number of sample points taken along each envelope edge when reprojecting a bounding box.</summary>
    private const int EdgeSamples = 8;

    private static readonly CoordinateTransformationFactory TransformFactory = new();
    private static readonly CoordinateSystemFactory CsFactory = new();

    private readonly ConcurrentDictionary<(CoordinateReferenceSystem Source, CoordinateReferenceSystem Target), MathTransform> _cache = new();

    public Geometry Reproject(Geometry geometry, CoordinateReferenceSystem source, CoordinateReferenceSystem target)
    {
        var copy = geometry.Copy();
        if (!source.Equals(target))
        {
            copy.Apply(new MathTransformSequenceFilter(GetTransform(source, target)));
        }

        copy.SRID = target.Srid;
        copy.GeometryChanged();
        return copy;
    }

    public Envelope Reproject(Envelope envelope, CoordinateReferenceSystem source, CoordinateReferenceSystem target)
    {
        if (source.Equals(target) || envelope.IsNull)
        {
            return envelope.Copy();
        }

        var transform = GetTransform(source, target);
        var result = new Envelope();

        // Sample the whole boundary (not just the four corners) so a curved/rotated projection is fully
        // enclosed by the reprojected box.
        for (var i = 0; i <= EdgeSamples; i++)
        {
            var t = (double)i / EdgeSamples;
            var x = envelope.MinX + (envelope.MaxX - envelope.MinX) * t;
            var y = envelope.MinY + (envelope.MaxY - envelope.MinY) * t;
            result.ExpandToInclude(Project(transform, x, envelope.MinY));
            result.ExpandToInclude(Project(transform, x, envelope.MaxY));
            result.ExpandToInclude(Project(transform, envelope.MinX, y));
            result.ExpandToInclude(Project(transform, envelope.MaxX, y));
        }

        return result;
    }

    private static Coordinate Project(MathTransform transform, double x, double y)
    {
        var (px, py) = transform.Transform(x, y);
        return new Coordinate(px, py);
    }

    private MathTransform GetTransform(CoordinateReferenceSystem source, CoordinateReferenceSystem target)
        => _cache.GetOrAdd((source, target),
            key => TransformFactory.CreateFromCoordinateSystems(Resolve(key.Source), Resolve(key.Target)).MathTransform);

    private static CoordinateSystem Resolve(CoordinateReferenceSystem crs)
    {
        if (!string.IsNullOrWhiteSpace(crs.Wkt))
        {
            return CsFactory.CreateFromWkt(crs.Wkt);
        }

        return crs.Srid switch
        {
            4326 => GeographicCoordinateSystem.WGS84,
            3857 => ProjectedCoordinateSystem.WebMercator,
            _ => throw new NotSupportedException(
                $"SRID {crs.Srid} is not a built-in CRS (4326 or 3857). Set the layer's Provider.SourceCrsWkt to its WKT definition."),
        };
    }

    /// <summary>Applies a ProjNET <see cref="MathTransform"/> to every ordinate in place, preserving structure.</summary>
    private sealed class MathTransformSequenceFilter : ICoordinateSequenceFilter
    {
        private readonly MathTransform _transform;

        public MathTransformSequenceFilter(MathTransform transform) => _transform = transform;

        public bool Done => false;
        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int index)
        {
            var (x, y) = _transform.Transform(seq.GetX(index), seq.GetY(index));
            seq.SetX(index, x);
            seq.SetY(index, y);
        }
    }
}
