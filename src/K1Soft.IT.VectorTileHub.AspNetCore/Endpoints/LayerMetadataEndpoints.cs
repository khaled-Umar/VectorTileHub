using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class LayerMetadataEndpoints
{
    public static void MapLayerMetadataEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)
    {
        endpoints.MapGet($"{options.RoutePrefix}/layers", (IVectorTileLayerConfigProvider provider) =>
        {
            var layers = provider.GetAllLayers()
                .Where(x => x.Enabled)
                .Select(x => ToDto(x, options.RoutePrefix))
                .ToArray();

            return Results.Ok(new { layers });
        });

        endpoints.MapGet($"{options.RoutePrefix}/layers/{{layerId:int}}", (int layerId, IVectorTileLayerConfigProvider provider) =>
        {
            var layer = provider.GetLayer(layerId);
            return layer is null || !layer.Enabled
                ? Results.NotFound(new { error = "Layer not found" })
                : Results.Ok(ToDto(layer, options.RoutePrefix));
        });
    }

    private static LayerMetadataDto ToDto(VectorTileLayerConfig layer, string routePrefix)
    {
        return new LayerMetadataDto(
            layer.Id,
            layer.LayerKey,
            layer.LayerName,
            layer.Tile.MinZoom,
            layer.Tile.MaxZoom,
            $"{routePrefix}/tiles/{layer.Id}/{{z}}/{{x}}/{{y}}.pbf");
    }

    private sealed record LayerMetadataDto(int Id, string LayerKey, string LayerName, int MinZoom, int MaxZoom, string TileUrlTemplate);
}
