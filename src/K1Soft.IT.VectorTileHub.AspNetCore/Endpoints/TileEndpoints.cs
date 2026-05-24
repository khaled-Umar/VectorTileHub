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
                var scope = httpContext.Request.Query["scope"].FirstOrDefault();
                var result = await tileService.GetTileAsync(layerId, z, x, y, httpContext.User, scope, cancellationToken);
                return ToHttpResult(result);
            });
    }

    private static IResult ToHttpResult(VectorTileResult result)
    {
        return result.Status switch
        {
            VectorTileResultStatus.Ok => Results.File(result.TileBytes, result.ContentType),
            VectorTileResultStatus.BadRequest => Results.BadRequest(new { error = result.Error }),
            VectorTileResultStatus.Unauthorized => Results.Unauthorized(),
            VectorTileResultStatus.Forbidden => Results.Forbid(),
            VectorTileResultStatus.NotFound => Results.NotFound(new { error = result.Error }),
            VectorTileResultStatus.ServiceUnavailable => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Json(new { error = result.Error ?? "Unexpected tile error" }, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
