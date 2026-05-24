using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class AdminConfigEndpoints
{
    public static void MapAdminConfigEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)
    {
        endpoints.MapPost($"{options.RoutePrefix}/admin/layers/reload", async (IVectorTileLayerConfigProvider provider, CancellationToken cancellationToken) =>
        {
            await provider.ReloadAsync(cancellationToken);
            var layers = provider.GetAllLayers();
            return Results.Ok(new
            {
                layersLoaded = layers.Count,
                layersEnabled = layers.Count(x => x.Enabled),
                layersDisabled = layers.Count(x => !x.Enabled),
                errors = Array.Empty<object>()
            });
        }).RequireAuthorization();
    }
}
