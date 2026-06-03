using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.Sample.Controllers;

// Host-owned layer-metadata endpoints. Calls the library's IVectorTileLayerConfigProvider and shapes
// the DTO the frontend expects. The tile URL template points at THIS host's tile route.
[ApiController]
[Route("vector-tile-hub")]
[Tags("Layers")]
public sealed class LayersController : ControllerBase
{
    // Matches TilesController's route; the frontend builds tile requests from this template.
    private const string TileRouteBase = "/vector-tile-hub";

    private readonly IVectorTileLayerConfigProvider _provider;

    public LayersController(IVectorTileLayerConfigProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("layers")]
    public IActionResult GetLayers()
    {
        var layers = _provider.GetAllLayers()
            .Where(x => x.Enabled)
            .Select(ToDto)
            .ToArray();

        return Ok(new { layers });
    }

    [HttpGet("layers/{layerId:int}")]
    public IActionResult GetLayer(int layerId)
    {
        var layer = _provider.GetLayer(layerId);
        return layer is null || !layer.Enabled
            ? NotFound(new { error = "Layer not found" })
            : Ok(ToDto(layer));
    }

    private static LayerMetadataDto ToDto(VectorTileLayerConfig layer)
    {
        var variants = layer.CacheRules.Count > 0
            ? layer.CacheRules.Select(r => r.VariantKey).ToArray()
            : [VectorTileVariant.DefaultKey];

        return new LayerMetadataDto(
            layer.Id,
            layer.LayerKey,
            layer.LayerName,
            layer.Tile.MinZoom,
            layer.Tile.MaxZoom,
            $"{TileRouteBase}/tiles/{layer.Id}/{{z}}/{{x}}/{{y}}.pbf",
            variants,
            layer.Attributes.Include);
    }

    private sealed record LayerMetadataDto(
        int Id,
        string LayerKey,
        string LayerName,
        int MinZoom,
        int MaxZoom,
        string TileUrlTemplate,
        string[] Variants,
        string[] Attributes);
}
