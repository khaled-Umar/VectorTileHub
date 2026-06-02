using K1Soft.IT.VectorTileHub;
using NetTopologySuite.Geometries;
using Xunit;

namespace K1Soft.IT.VectorTileHub.Core.Tests;

public class TileCoordinateUtilsTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(1, 1, 1, true)]
    [InlineData(2, 4, 0, false)] // x out of range at z=2 (max index 3)
    [InlineData(-1, 0, 0, false)]
    public void IsValidTile_ChecksBounds(int z, int x, int y, bool expected)
    {
        Assert.Equal(expected, TileCoordinateUtils.IsValidTile(z, x, y));
    }

    [Fact]
    public void GetTileEnvelope_Z0_CoversWholeWorld()
    {
        var env = TileCoordinateUtils.GetTileEnvelope(0, 0, 0);
        Assert.True(env.MinX < -20037508);
        Assert.True(env.MaxX > 20037508);
    }

    [Fact]
    public void GetAffectedTilesForZoomRange_CoversEachZoom()
    {
        var bbox = TileCoordinateUtils.GetTileEnvelope(4, 8, 8);
        var tiles = TileCoordinateUtils.GetAffectedTilesForZoomRange(bbox, 4, 6).ToList();

        Assert.Contains(tiles, t => t.z == 4);
        Assert.Contains(tiles, t => t.z == 5);
        Assert.Contains(tiles, t => t.z == 6);
        Assert.All(tiles, t => Assert.InRange(t.z, 4, 6));
    }

    [Fact]
    public void ExpandEnvelope_AddsBuffer()
    {
        var env = new Envelope(0, 100, 0, 100);
        var expanded = TileCoordinateUtils.ExpandEnvelope(env, 64, 4096);
        Assert.True(expanded.Width > env.Width);
        Assert.True(expanded.Height > env.Height);
    }
}
