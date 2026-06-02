using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

// NOTE: No built-in authorization — the host secures this endpoint.
public static class AdminConfigEndpoints
{
    public static void MapAdminConfigEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)
    {
        endpoints.MapPost($"{options.RoutePrefix}/admin/config/reload", async (IVectorTileLayerConfigProvider provider, CancellationToken cancellationToken) =>
        {
            await provider.ReloadAsync(cancellationToken);
            var layers = provider.GetAllLayers();
            var failed = provider is JsonLayerConfigProvider jsonProvider
                ? jsonProvider.LastResult.Failed.Select(f => new { path = f.Path, error = f.Error }).ToArray()
                : [];

            return Results.Ok(new
            {
                loaded = layers.Select(x => x.Id).ToArray(),
                layersEnabled = layers.Count(x => x.Enabled),
                layersDisabled = layers.Count(x => !x.Enabled),
                failed
            });
        }).WithTags("Admin Config");
    }
}
