using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.Sample.Controllers;

// Host-owned config-reload endpoint. Reloads the layer JSON files from disk via the library's
// IVectorTileLayerConfigProvider. Gated by the host's own policy.
[ApiController]
[Route("vector-tile-hub/admin/config")]
[Tags("Admin Config")]
[Authorize(Roles = "GISAdmin")]
public sealed class ConfigAdminController : ControllerBase
{
    private readonly IVectorTileLayerConfigProvider _provider;

    public ConfigAdminController(IVectorTileLayerConfigProvider provider)
    {
        _provider = provider;
    }

    [HttpPost("reload")]
    public async Task<IActionResult> Reload(CancellationToken cancellationToken)
    {
        await _provider.ReloadAsync(cancellationToken);
        var layers = _provider.GetAllLayers();
        var failed = _provider is JsonLayerConfigProvider jsonProvider
            ? jsonProvider.LastResult.Failed.Select(f => new { path = f.Path, error = f.Error }).ToArray()
            : [];

        return Ok(new
        {
            loaded = layers.Select(x => x.Id).ToArray(),
            layersEnabled = layers.Count(x => x.Enabled),
            layersDisabled = layers.Count(x => !x.Enabled),
            failed
        });
    }
}
