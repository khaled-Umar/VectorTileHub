using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class TileEndpoints
{
    public static void MapTileEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)
    {
        endpoints.MapGet($"{options.RoutePrefix}/tiles/{{layerId:int}}/{{z:int}}/{{x:int}}/{{y:int}}.pbf",
            async (int layerId, int z, int x, int y, HttpContext httpContext, IVectorTileService tileService, CancellationToken cancellationToken) =>
            {
                var variant = httpContext.Request.Query["variant"].FirstOrDefault();
                var result = await tileService.GetTileAsync(layerId, z, x, y, variant, cancellationToken);

                if (result.Status == VectorTileResultStatus.Ok)
                {
                    httpContext.Response.Headers["X-VTH-From-Cache"] = result.FromCache ? "true" : "false";
                    httpContext.Response.Headers["X-VTH-Stale"] = result.IsStale ? "true" : "false";
                }

                return ToHttpResult(result);
            })
            .WithName("GetVectorTile")
            .WithTags("Tiles");
    }

    private static IResult ToHttpResult(VectorTileResult result)
    {
        return result.Status switch
        {
            VectorTileResultStatus.Ok => Results.File(result.TileBytes, result.ContentType),
            VectorTileResultStatus.BadRequest => Results.BadRequest(new { error = result.Error }),
            VectorTileResultStatus.NotFound => Results.NotFound(new { error = result.Error }),
            VectorTileResultStatus.ServiceUnavailable => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Json(new { error = result.Error ?? "Unexpected tile error" }, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
