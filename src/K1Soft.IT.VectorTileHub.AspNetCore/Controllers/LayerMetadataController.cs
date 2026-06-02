using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.AspNetCore.Controllers;

[ApiController]
[Tags("Layers")]
public sealed class LayerMetadataController : ControllerBase
{
    private readonly IVectorTileLayerConfigProvider _provider;
    private readonly VectorTileHubOptions _options;

    public LayerMetadataController(IVectorTileLayerConfigProvider provider, IOptions<VectorTileHubOptions> options)
    {
        _provider = provider;
        _options = options.Value;
    }

    [HttpGet("layers")]
    public IActionResult GetLayers()
    {
        var layers = _provider.GetAllLayers()
            .Where(x => x.Enabled)
            .Select(x => ToDto(x, _options.RoutePrefix))
            .ToArray();

        return Ok(new { layers });
    }

    [HttpGet("layers/{layerId:int}")]
    public IActionResult GetLayer(int layerId)
    {
        var layer = _provider.GetLayer(layerId);
        return layer is null || !layer.Enabled
            ? NotFound(new { error = "Layer not found" })
            : Ok(ToDto(layer, _options.RoutePrefix));
    }

    private static LayerMetadataDto ToDto(VectorTileLayerConfig layer, string routePrefix)
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
            $"{routePrefix}/tiles/{layer.Id}/{{z}}/{{x}}/{{y}}.pbf",
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
