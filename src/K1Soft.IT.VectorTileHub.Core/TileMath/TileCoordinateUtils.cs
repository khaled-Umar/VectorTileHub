using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public static class TileCoordinateUtils
{
    private const double OriginShift = 20037508.342789244;

    public static bool IsValidTile(int z, int x, int y)
    {
        if (z < 0 || z > 31)
        {
            return false;
        }

        var max = 1 << z;
        return x >= 0 && y >= 0 && x < max && y < max;
    }

    public static Envelope GetTileEnvelope(int z, int x, int y)
    {
        if (!IsValidTile(z, x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Invalid XYZ tile coordinate.");
        }

        var tiles = Math.Pow(2, z);
        var tileSize = (OriginShift * 2) / tiles;
        var minX = -OriginShift + x * tileSize;
        var maxX = minX + tileSize;
        var maxY = OriginShift - y * tileSize;
        var minY = maxY - tileSize;
        return new Envelope(minX, maxX, minY, maxY);
    }

    public static Envelope ExpandEnvelope(Envelope env, int buffer, int extent)
    {
        if (buffer <= 0 || extent <= 0)
        {
            return new Envelope(env);
        }

        var xPad = env.Width * buffer / extent;
        var yPad = env.Height * buffer / extent;
        return new Envelope(env.MinX - xPad, env.MaxX + xPad, env.MinY - yPad, env.MaxY + yPad);
    }

    public static IEnumerable<(int z, int x, int y)> GetAffectedTiles(Envelope bbox, int zoom)
    {
        if (zoom < 0 || zoom > 31)
        {
            yield break;
        }

        var min = WorldToTile(bbox.MinX, bbox.MaxY, zoom);
        var max = WorldToTile(bbox.MaxX, bbox.MinY, zoom);
        var limit = (1 << zoom) - 1;

        for (var x = Math.Clamp(min.x, 0, limit); x <= Math.Clamp(max.x, 0, limit); x++)
        {
            for (var y = Math.Clamp(min.y, 0, limit); y <= Math.Clamp(max.y, 0, limit); y++)
            {
                yield return (zoom, x, y);
            }
        }
    }

    public static IEnumerable<(int z, int x, int y)> GetAffectedTilesForZoomRange(Envelope bbox, int minZoom, int maxZoom)
    {
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            foreach (var tile in GetAffectedTiles(bbox, zoom))
            {
                yield return tile;
            }
        }
    }

    /// <summary>
    /// Converts a configured layer extent to a Web Mercator (EPSG:3857) envelope for tile math.
    /// Supports source SRIDs 4326 (lon/lat) and 3857 (mercator).
    /// </summary>
    public static Envelope ToMercatorEnvelope(ExtentConfig extent)
    {
        switch (extent.Srid)
        {
            case 3857:
                return new Envelope(extent.MinX, extent.MaxX, extent.MinY, extent.MaxY);
            case 4326:
                var (minX, minY) = LonLatToMercator(extent.MinX, extent.MinY);
                var (maxX, maxY) = LonLatToMercator(extent.MaxX, extent.MaxY);
                return new Envelope(minX, maxX, minY, maxY);
            default:
                throw new NotSupportedException(
                    $"Extent SRID {extent.Srid} is not supported; use 4326 (lon/lat) or 3857 (Web Mercator).");
        }
    }

    public static (double x, double y) LonLatToMercator(double lon, double lat)
    {
        var clampedLat = Math.Clamp(lat, -85.05112878, 85.05112878);
        var x = lon * OriginShift / 180.0;
        var y = Math.Log(Math.Tan((90.0 + clampedLat) * Math.PI / 360.0)) / (Math.PI / 180.0) * OriginShift / 180.0;
        return (x, y);
    }

    /// <summary>
    /// Counts (without enumerating) the tiles intersecting <paramref name="bbox"/> across the zoom
    /// range. Used to compute generation progress without materializing the full tile list.
    /// </summary>
    public static long CountAffectedTiles(Envelope bbox, int minZoom, int maxZoom)
    {
        long total = 0;
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            if (zoom < 0 || zoom > 31)
            {
                continue;
            }

            var min = WorldToTile(bbox.MinX, bbox.MaxY, zoom);
            var max = WorldToTile(bbox.MaxX, bbox.MinY, zoom);
            var limit = (1 << zoom) - 1;
            var x0 = Math.Clamp(min.x, 0, limit);
            var x1 = Math.Clamp(max.x, 0, limit);
            var y0 = Math.Clamp(min.y, 0, limit);
            var y1 = Math.Clamp(max.y, 0, limit);
            total += (long)(x1 - x0 + 1) * (y1 - y0 + 1);
        }

        return total;
    }

    private static (int x, int y) WorldToTile(double mx, double my, int zoom)
    {
        var tiles = Math.Pow(2, zoom);
        var x = (int)Math.Floor((mx + OriginShift) / (2 * OriginShift) * tiles);
        var y = (int)Math.Floor((OriginShift - my) / (2 * OriginShift) * tiles);
        return (x, y);
    }
}
