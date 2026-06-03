using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.Sample.Controllers;

// Host-owned tile endpoint. The library ships no controllers — this host exposes the route it wants
// and calls the library's IVectorTileService. Left anonymous (public read); see the variant note below
// for where a host would gate access or map a user role to a cache variant.
[ApiController]
[Route("vector-tile-hub")]
[Tags("Tiles")]
public sealed class TilesController : ControllerBase
{
    private readonly IVectorTileService _tileService;

    public TilesController(IVectorTileService tileService)
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
        // The host decides the variant. Here we honour an explicit ?variant= query, but a real host
        // could instead derive it from the caller's identity, e.g.:
        //   variant = User.IsInRole("Resident") ? "residential" : null;
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
