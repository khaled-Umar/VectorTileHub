using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.AspNetCore.Controllers;

// NOTE: No built-in authorization — the host secures this endpoint.
[ApiController]
[Tags("Admin Config")]
public sealed class AdminConfigController : ControllerBase
{
    private readonly IVectorTileLayerConfigProvider _provider;

    public AdminConfigController(IVectorTileLayerConfigProvider provider)
    {
        _provider = provider;
    }

    [HttpPost("admin/config/reload")]
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
