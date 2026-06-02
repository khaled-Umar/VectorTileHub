using Hangfire;
using K1Soft.IT.VectorTileHub.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub.AspNetCore.Controllers;

// NOTE: The library applies NO authorization here. The host is responsible for
// securing these admin endpoints (and the job dashboard) with its own policy.
[ApiController]
[Route("admin/layers")]
[Tags("Admin Cache")]
public sealed class AdminCacheController : ControllerBase
{
    [HttpPost("{layerId:int}/cache/generate")]
    public IActionResult Generate(int layerId, [FromBody] CacheGenerateRequest? request, [FromServices] IBackgroundJobClient jobs)
    {
        var jobId = jobs.Enqueue<CacheGenerationJob>(job => job.Execute(layerId, request == null ? null : request.MinZoom, request == null ? null : request.MaxZoom, request == null ? null : request.Variants, request == null ? null : request.MaxDegreeOfParallelism, null, CancellationToken.None));
        return Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache generation job enqueued" });
    }

    [HttpPost("{layerId:int}/cache/delete")]
    public IActionResult Delete(int layerId, [FromBody] CacheDeleteRequest? request, [FromServices] IBackgroundJobClient jobs)
    {
        var cacheVersion = request == null ? null : request.CacheVersion;
        var deleteAllVersions = request is { DeleteAllVersions: true };
        var jobId = jobs.Enqueue<CacheDeletionJob>(job => job.Execute(layerId, cacheVersion, deleteAllVersions, CancellationToken.None));
        return Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache deletion job enqueued" });
    }

    [HttpPost("{layerId:int}/cache/invalidate")]
    public async Task<IActionResult> Invalidate(
        int layerId,
        [FromBody] CacheInvalidateRequest request,
        [FromServices] IVectorTileRuntimeSettingsStore store,
        [FromServices] IVectorTileLayerConfigProvider layers,
        [FromServices] IVectorTileCache cache,
        CancellationToken cancellationToken)
    {
        var layer = layers.GetLayer(layerId);
        if (layer is null)
        {
            return NotFound(new { error = "Layer not found" });
        }

        var runtime = await store.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
        var envelope = request.BoundingBox.ToEnvelope();
        var tiles = TileCoordinateUtils.GetAffectedTilesForZoomRange(envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom).ToArray();
        var variants = request.Variants is { Length: > 0 }
            ? request.Variants
            : layer.CacheRules.Count > 0 ? layer.CacheRules.Select(r => r.VariantKey).ToArray() : [VectorTileVariant.DefaultKey];
        foreach (var variant in variants)
        {
            await cache.RemoveByEnvelopeAsync(layerId, envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom, variant, runtime.ActiveCacheVersion, cancellationToken);
        }

        runtime.LastInvalidatedAt = DateTimeOffset.UtcNow;
        await store.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
        return Ok(new { layerId, tilesInvalidated = tiles.Length * variants.Length, zoomLevelsAffected = tiles.Select(x => x.z).Distinct().Order().ToArray() });
    }

    [HttpPost("{layerId:int}/cache/notify-change")]
    public IActionResult NotifyChange(int layerId, [FromBody] CacheNotifyChangeRequest request, [FromServices] IBackgroundJobClient jobs)
    {
        var jobId = jobs.Enqueue<CacheInvalidationJob>(job => job.Execute(layerId, request.BoundingBox.MinX, request.BoundingBox.MinY, request.BoundingBox.MaxX, request.BoundingBox.MaxY, request.BoundingBox.Srid, request.Variants, CancellationToken.None));
        return Accepted(value: new { layerId, jobId, message = "Tiles refresh accepted" });
    }

    [HttpPost("{layerId:int}/cache/swap")]
    public IActionResult Swap(int layerId, [FromBody] CacheSwapRequest? request, [FromServices] IBackgroundJobClient jobs)
    {
        var version = string.IsNullOrWhiteSpace(request?.NewVersion) ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") : request.NewVersion;
        var regenerateAfterSwap = request is null || request.RegenerateAfterSwap;
        var deleteOldVersion = request is null || request.DeleteOldVersion;
        var jobId = jobs.Enqueue<CacheSwapJob>(job => job.Execute(layerId, version, regenerateAfterSwap, deleteOldVersion, CancellationToken.None));
        return Accepted(value: new { layerId, newVersion = version, status = "Swapped", jobId });
    }

    [HttpGet("{layerId:int}/cache/status")]
    public async Task<IActionResult> Status(int layerId, [FromServices] IVectorTileRuntimeSettingsStore store, CancellationToken cancellationToken)
    {
        var settings = await store.GetLayerRuntimeSettingsAsync(layerId, cancellationToken);
        return settings is null ? NotFound(new { error = "Layer not found" }) : Ok(settings);
    }
}

public sealed record CacheGenerateRequest(int? MinZoom, int? MaxZoom, string[]? Variants, int? MaxDegreeOfParallelism);

public sealed record CacheDeleteRequest(string? CacheVersion, bool DeleteAllVersions);

public sealed record CacheInvalidateRequest(BoundingBoxDto BoundingBox, string[]? Variants);

public sealed record CacheNotifyChangeRequest(BoundingBoxDto BoundingBox, string[]? Variants);

public sealed record CacheSwapRequest(string? NewVersion, bool RegenerateAfterSwap, bool DeleteOldVersion);

public sealed record BoundingBoxDto(double MinX, double MinY, double MaxX, double MaxY, int Srid)
{
    public Envelope ToEnvelope() => new(MinX, MaxX, MinY, MaxY);
}
