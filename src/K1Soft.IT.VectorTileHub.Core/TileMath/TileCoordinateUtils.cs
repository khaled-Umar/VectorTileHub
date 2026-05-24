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

    private static (int x, int y) WorldToTile(double mx, double my, int zoom)
    {
        var tiles = Math.Pow(2, zoom);
        var x = (int)Math.Floor((mx + OriginShift) / (2 * OriginShift) * tiles);
        var y = (int)Math.Floor((OriginShift - my) / (2 * OriginShift) * tiles);
        return (x, y);
    }
}
