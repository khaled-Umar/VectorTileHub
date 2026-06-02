using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.AspNetCore.Controllers;

[ApiController]
[Tags("Tiles")]
public sealed class VectorTileController : ControllerBase
{
    private readonly IVectorTileService _tileService;

    public VectorTileController(IVectorTileService tileService)
    {
        _tileService = tileService;
    }

    [HttpGet("tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf", Name = "GetVectorTile")]
    public async Task<IActionResult> GetTile(
        int layerId,
        int z,
        int x,
        int y,
        [FromQuery] string? variant,
        CancellationToken cancellationToken)
    {
        var result = await _tileService.GetTileAsync(layerId, z, x, y, variant, cancellationToken);

        if (result.Status == VectorTileResultStatus.Ok)
        {
            Response.Headers["X-VTH-From-Cache"] = result.FromCache ? "true" : "false";
            Response.Headers["X-VTH-Stale"] = result.IsStale ? "true" : "false";
        }

        return result.Status switch
        {
            VectorTileResultStatus.Ok => File(result.TileBytes, result.ContentType),
            VectorTileResultStatus.NoContent => NoContent(),
            VectorTileResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            VectorTileResultStatus.NotFound => NotFound(new { error = result.Error }),
            VectorTileResultStatus.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error ?? "Unexpected tile error" })
        };
    }
}
