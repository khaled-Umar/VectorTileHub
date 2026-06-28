using K1Soft.IT.VectorTileHub;
using NetTopologySuite.Geometries;
using Xunit;

namespace K1Soft.IT.VectorTileHub.Core.Tests;

public class ProjNetCoordinateReprojectorTests
{
    // Jeddah NGN zone 37M (local SRID 10000): UTM-37N-like Transverse Mercator on WGS 84 with a survey-fitted
    // false easting/northing. The same definition the importer uses; a custom CRS only VTH-with-WKT can serve.
    private const int NgnSrid = 10000;
    private const string NgnWkt =
        "PROJCS[\"WGS 84 / NGN zone 37M\"," +
        "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0]," +
        "UNIT[\"degree\",0.0174532925199433]]," +
        "PROJECTION[\"Transverse_Mercator\"]," +
        "PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",39],PARAMETER[\"scale_factor\",0.9996]," +
        "PARAMETER[\"false_easting\",500196.54],PARAMETER[\"false_northing\",98.17]," +
        "UNIT[\"metre\",1]]";

    private static readonly GeometryFactory Geometries = new();
    private readonly ProjNetCoordinateReprojector _reprojector = new();

    [Fact]
    public void Reproject_SameCrs_CopiesThroughAndStampsSrid()
    {
        var point = Geometries.CreatePoint(new Coordinate(500_000, 2_376_000));

        var result = _reprojector.Reproject(point, new CoordinateReferenceSystem(NgnSrid, NgnWkt), new CoordinateReferenceSystem(NgnSrid, NgnWkt));

        Assert.Equal(NgnSrid, result.SRID);
        Assert.Equal(500_000, result.Coordinate.X, 6);
        Assert.Equal(2_376_000, result.Coordinate.Y, 6);
    }

    [Fact]
    public void Reproject_CustomCrsToWebMercatorAndBack_RoundTrips()
    {
        // A point near Jeddah expressed in NGN metres.
        var ngn = new CoordinateReferenceSystem(NgnSrid, NgnWkt);
        var original = Geometries.CreatePoint(new Coordinate(500_500, 2_376_500));

        var mercator = _reprojector.Reproject(original, ngn, CoordinateReferenceSystem.WebMercator);
        var back = _reprojector.Reproject(mercator, CoordinateReferenceSystem.WebMercator, ngn);

        Assert.Equal(3857, mercator.SRID);
        Assert.Equal(NgnSrid, back.SRID);
        // Sub-millimetre after a full forward+inverse through the custom projection.
        Assert.Equal(original.Coordinate.X, back.Coordinate.X, 3);
        Assert.Equal(original.Coordinate.Y, back.Coordinate.Y, 3);
    }

    [Fact]
    public void Reproject_4326ToWebMercator_MatchesSphericalMercator()
    {
        const double lon = 39.2;
        const double lat = 21.5;
        const double originShift = 20037508.342789244;
        var expectedX = lon * originShift / 180.0;
        var expectedY = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0) * originShift / 180.0;

        var result = _reprojector.Reproject(
            Geometries.CreatePoint(new Coordinate(lon, lat)),
            CoordinateReferenceSystem.Wgs84,
            CoordinateReferenceSystem.WebMercator);

        // Parity with the previous hardcoded Web-Mercator math so existing 4326 layers don't shift.
        Assert.Equal(expectedX, result.Coordinate.X, 0.5);
        Assert.Equal(expectedY, result.Coordinate.Y, 0.5);
    }

    [Fact]
    public void Reproject_Envelope_WebMercatorToWgs84_CoversExpectedLonLat()
    {
        const double originShift = 20037508.342789244;
        double MercX(double lon) => lon * originShift / 180.0;
        double MercY(double lat) => Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0) * originShift / 180.0;
        // A small box around lon 39, lat 21 in Web Mercator.
        var mercEnvelope = new Envelope(MercX(38.9), MercX(39.1), MercY(20.9), MercY(21.1));

        var geographic = _reprojector.Reproject(mercEnvelope, CoordinateReferenceSystem.WebMercator, CoordinateReferenceSystem.Wgs84);

        Assert.InRange(geographic.MinX, 38.89, 38.91);
        Assert.InRange(geographic.MaxX, 39.09, 39.11);
        Assert.InRange(geographic.MinY, 20.89, 20.91);
        Assert.InRange(geographic.MaxY, 21.09, 21.11);
    }

    [Fact]
    public void Reproject_UnknownSridWithoutWkt_Throws()
    {
        var point = Geometries.CreatePoint(new Coordinate(500_000, 2_376_000));

        Assert.Throws<NotSupportedException>(() =>
            _reprojector.Reproject(point, new CoordinateReferenceSystem(NgnSrid), CoordinateReferenceSystem.WebMercator));
    }
}
