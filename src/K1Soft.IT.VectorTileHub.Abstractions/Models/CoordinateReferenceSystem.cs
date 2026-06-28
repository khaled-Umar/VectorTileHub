namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// A coordinate reference system identified by its SRID, optionally carrying an explicit WKT definition for
/// CRSs that are not built in (anything other than 4326 / 3857). When <see cref="Wkt"/> is supplied it takes
/// precedence over <see cref="Srid"/> when resolving the projection parameters, which is how local/custom
/// projections (e.g. a survey-fitted Transverse Mercator) are supported. The SRID is still carried so it can
/// be stamped onto the reprojected geometry / used as the database SRID for the spatial filter.
/// </summary>
public readonly record struct CoordinateReferenceSystem(int Srid, string? Wkt = null)
{
    /// <summary>WGS 84 geographic lon/lat (EPSG:4326).</summary>
    public static readonly CoordinateReferenceSystem Wgs84 = new(4326);

    /// <summary>WGS 84 / Pseudo-Mercator (EPSG:3857) — the CRS tiles are served in.</summary>
    public static readonly CoordinateReferenceSystem WebMercator = new(3857);

    /// <summary>The source CRS declared by a layer's provider configuration.</summary>
    public static CoordinateReferenceSystem FromProvider(ProviderConfig provider)
        => new(provider.SourceSrid, provider.SourceCrsWkt);
}
