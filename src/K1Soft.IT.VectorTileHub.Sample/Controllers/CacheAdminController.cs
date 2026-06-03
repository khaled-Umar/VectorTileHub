using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace K1Soft.IT.VectorTileHub.Sample.Controllers;

// Host-owned cache-admin endpoints. The library exposes these operations as IVectorTileCacheAdmin;
// THIS host decides to surface them under /vector-tile-hub/admin/layers and gate them with its own
// policy. The HTTP request shapes are the host's concern, so the DTOs live here.
[ApiController]
[Route("vector-tile-hub/admin/layers")]
[Tags("Admin Cache")]
[Authorize(Roles = "GISAdmin")]
public sealed class CacheAdminController : ControllerBase
{
    private readonly IVectorTileCacheAdmin _admin;

    public CacheAdminController(IVectorTileCacheAdmin admin)
    {
        _admin = admin;
    }

    [HttpPost("{layerId:int}/cache/generate")]
    public IActionResult Generate(int layerId, [FromBody] CacheGenerateRequest? request)
    {
        var jobId = _admin.EnqueueGenerate(layerId, request?.MinZoom, request?.MaxZoom, request?.Variants, request?.MaxDegreeOfParallelism);
        return Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache generation job enqueued" });
    }

    [HttpPost("{layerId:int}/cache/delete")]
    public IActionResult Delete(int layerId, [FromBody] CacheDeleteRequest? request)
    {
        var jobId = _admin.EnqueueDelete(layerId, request?.CacheVersion, request is { DeleteAllVersions: true });
        return Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache deletion job enqueued" });
    }

    [HttpPost("{layerId:int}/cache/invalidate")]
    public async Task<IActionResult> Invalidate(int layerId, [FromBody] CacheInvalidateRequest request, CancellationToken cancellationToken)
    {
        var bbox = request.BoundingBox;
        var result = await _admin.InvalidateAsync(layerId, bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY, bbox.Srid, request.Variants, cancellationToken);
        return result is null
            ? NotFound(new { error = "Layer not found" })
            : Ok(new { layerId, tilesInvalidated = result.TilesInvalidated, zoomLevelsAffected = result.ZoomLevelsAffected });
    }

    [HttpPost("{layerId:int}/cache/notify-change")]
    public IActionResult NotifyChange(int layerId, [FromBody] CacheNotifyChangeRequest request)
    {
        var bbox = request.BoundingBox;
        var jobId = _admin.EnqueueNotifyChange(layerId, bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY, bbox.Srid, request.Variants);
        return Accepted(value: new { layerId, jobId, message = "Tiles refresh accepted" });
    }

    [HttpPost("{layerId:int}/cache/swap")]
    public IActionResult Swap(int layerId, [FromBody] CacheSwapRequest? request)
    {
        var result = _admin.EnqueueSwap(layerId, request?.NewVersion, request is null || request.RegenerateAfterSwap, request is null || request.DeleteOldVersion);
        return Accepted(value: new { layerId, newVersion = result.NewVersion, status = "Swapped", jobId = result.JobId });
    }

    [HttpGet("{layerId:int}/cache/status")]
    public async Task<IActionResult> Status(int layerId, CancellationToken cancellationToken)
    {
        var settings = await _admin.GetStatusAsync(layerId, cancellationToken);
        return settings is null ? NotFound(new { error = "Layer not found" }) : Ok(settings);
    }
}

public sealed record CacheGenerateRequest(int? MinZoom, int? MaxZoom, string[]? Variants, int? MaxDegreeOfParallelism);

public sealed record CacheDeleteRequest(string? CacheVersion, bool DeleteAllVersions);

public sealed record CacheInvalidateRequest(BoundingBoxDto BoundingBox, string[]? Variants);

public sealed record CacheNotifyChangeRequest(BoundingBoxDto BoundingBox, string[]? Variants);

public sealed record CacheSwapRequest(string? NewVersion, bool RegenerateAfterSwap, bool DeleteOldVersion);

public sealed record BoundingBoxDto(double MinX, double MinY, double MaxX, double MaxY, int Srid);
