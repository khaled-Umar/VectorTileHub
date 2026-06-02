using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.IO;

namespace K1Soft.IT.VectorTileHub.Sample.Controllers;

// Sample-only helper: returns the layer's data bounding box (EPSG:3857) so the
// OpenLayers page can zoom straight to the data instead of opening at world view.
// This is a host-owned controller; the VectorTileHub route convention only prefixes
// the library's own controllers, so this keeps its plain "/sample/..." route.
[ApiController]
[Route("sample/layers")]
public sealed class SampleExtentController : ControllerBase
{
    private readonly IVectorTileLayerConfigProvider _layers;
    private readonly IConfiguration _config;

    public SampleExtentController(IVectorTileLayerConfigProvider layers, IConfiguration config)
    {
        _layers = layers;
        _config = config;
    }

    [HttpGet("{id:int}/extent")]
    public async Task<IActionResult> GetExtent(int id, CancellationToken ct)
    {
        var layer = _layers.GetLayer(id);
        if (layer is null)
        {
            return NotFound(new { error = "Layer not found" });
        }

        // Prefer the configured extent — no database round-trip. Falls back to querying the data
        // bounds only when the layer has no extent configured.
        if (layer.Extent is { } configured)
        {
            var merc = TileCoordinateUtils.ToMercatorEnvelope(configured);
            return Ok(new { minX = merc.MinX, minY = merc.MinY, maxX = merc.MaxX, maxY = merc.MaxY, srid = 3857 });
        }

        var connectionString = !string.IsNullOrWhiteSpace(layer.Provider.ConnectionString)
            ? layer.Provider.ConnectionString
            : _config.GetConnectionString(layer.Provider.ConnectionStringName ?? "");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("No connection string configured for the layer.");
        }

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            var sql = $"SELECT geometry::EnvelopeAggregate(t.g).STAsText() FROM (SELECT TOP 5000 [{layer.Provider.GeometryColumn}] AS g FROM {layer.Provider.TableName} WHERE [{layer.Provider.GeometryColumn}] IS NOT NULL) t";
            await using var cmd = new SqlCommand(sql, conn);
            if (await cmd.ExecuteScalarAsync(ct) is not string wkt || string.IsNullOrWhiteSpace(wkt))
            {
                return NoContent();
            }

            var env = new WKTReader().Read(wkt).EnvelopeInternal;
            // Source SRID 4326 is stored as planar lon/lat; project corners to Web Mercator for the OL view.
            var (minX, minY) = layer.Provider.SourceSrid == 4326 ? LonLatToMercator(env.MinX, env.MinY) : (env.MinX, env.MinY);
            var (maxX, maxY) = layer.Provider.SourceSrid == 4326 ? LonLatToMercator(env.MaxX, env.MaxY) : (env.MaxX, env.MaxY);
            return Ok(new { minX, minY, maxX, maxY, srid = 3857 });
        }
        catch (Exception ex)
        {
            return Problem($"Extent query failed: {ex.Message}");
        }
    }

    private static (double x, double y) LonLatToMercator(double lon, double lat)
    {
        const double originShift = 20037508.342789244;
        var x = lon * originShift / 180.0;
        var y = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0) * originShift / 180.0;
        return (x, y);
    }
}
