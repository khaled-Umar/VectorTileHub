using Hangfire;
using K1Soft.IT.VectorTileHub.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public static class AdminCacheEndpoints
{
    public static void MapAdminCacheEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)
    {
        var group = endpoints.MapGroup($"{options.RoutePrefix}/admin/layers").RequireAuthorization();

        group.MapPost("/{layerId:int}/cache/generate", (int layerId, CacheGenerateRequest? request, IBackgroundJobClient jobs) =>
        {
            var jobId = jobs.Enqueue<CacheGenerationJob>(job => job.Execute(layerId, request == null ? null : request.MinZoom, request == null ? null : request.MaxZoom, request == null ? null : request.Scopes, CancellationToken.None));
            return Results.Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache generation job enqueued" });
        });

        group.MapPost("/{layerId:int}/cache/delete", (int layerId, CacheDeleteRequest? request, IBackgroundJobClient jobs) =>
        {
            var cacheVersion = request == null ? null : request.CacheVersion;
            var deleteAllVersions = request is { DeleteAllVersions: true };
            var jobId = jobs.Enqueue<CacheDeletionJob>(job => job.Execute(layerId, cacheVersion, deleteAllVersions, CancellationToken.None));
            return Results.Accepted(value: new { jobId, layerId, status = "Enqueued", message = "Cache deletion job enqueued" });
        });

        group.MapPost("/{layerId:int}/cache/invalidate", async (int layerId, CacheInvalidateRequest request, IVectorTileRuntimeSettingsStore store, IVectorTileLayerConfigProvider layers, IVectorTileCache cache, CancellationToken cancellationToken) =>
        {
            var layer = layers.GetLayer(layerId);
            if (layer is null)
            {
                return Results.NotFound(new { error = "Layer not found" });
            }

            var runtime = await store.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
            var envelope = request.BoundingBox.ToEnvelope();
            var tiles = TileCoordinateUtils.GetAffectedTilesForZoomRange(envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom).ToArray();
            var scopes = request.Scopes is { Length: > 0 } ? request.Scopes : ["public"];
            foreach (var scope in scopes)
            {
                await cache.RemoveByEnvelopeAsync(layerId, envelope, layer.Tile.MinZoom, layer.Tile.MaxZoom, scope, runtime.ActiveCacheVersion, cancellationToken);
            }

            runtime.LastInvalidatedAt = DateTimeOffset.UtcNow;
            await store.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);
            return Results.Ok(new { layerId, tilesInvalidated = tiles.Length * scopes.Length, zoomLevelsAffected = tiles.Select(x => x.z).Distinct().Order().ToArray() });
        });

        group.MapPost("/{layerId:int}/cache/notify-change", (int layerId, CacheNotifyChangeRequest request, IBackgroundJobClient jobs) =>
        {
            var jobId = request.Regenerate
                ? jobs.Enqueue<CacheInvalidationJob>(job => job.Execute(layerId, request.BoundingBox.MinX, request.BoundingBox.MinY, request.BoundingBox.MaxX, request.BoundingBox.MaxY, request.BoundingBox.Srid, null, CancellationToken.None))
                : null;
            return Results.Accepted(value: new { layerId, tilesAffected = 0, jobId, message = "Tiles invalidation accepted" });
        });

        group.MapPost("/{layerId:int}/cache/swap", (int layerId, CacheSwapRequest? request, IBackgroundJobClient jobs) =>
        {
            var version = string.IsNullOrWhiteSpace(request?.NewVersion) ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") : request.NewVersion;
            var regenerateAfterSwap = request is null || request.RegenerateAfterSwap;
            var deleteOldVersion = request is null || request.DeleteOldVersion;
            var jobId = jobs.Enqueue<CacheSwapJob>(job => job.Execute(layerId, version, regenerateAfterSwap, deleteOldVersion, CancellationToken.None));
            return Results.Accepted(value: new { layerId, newVersion = version, status = "Swapped", jobId });
        });

        group.MapGet("/{layerId:int}/cache/status", async (int layerId, IVectorTileRuntimeSettingsStore store, CancellationToken cancellationToken) =>
        {
            var settings = await store.GetLayerRuntimeSettingsAsync(layerId, cancellationToken);
            return settings is null ? Results.NotFound(new { error = "Layer not found" }) : Results.Ok(settings);
        });
    }

    private sealed record CacheGenerateRequest(int? MinZoom, int? MaxZoom, string[]? Scopes);
    private sealed record CacheDeleteRequest(string? CacheVersion, bool DeleteAllVersions);
    private sealed record CacheInvalidateRequest(BoundingBoxDto BoundingBox, string[]? Scopes);
    private sealed record CacheNotifyChangeRequest(BoundingBoxDto BoundingBox, string ChangeType, bool Regenerate);
    private sealed record CacheSwapRequest(string? NewVersion, bool RegenerateAfterSwap, bool DeleteOldVersion);

    private sealed record BoundingBoxDto(double MinX, double MinY, double MaxX, double MaxY, int Srid)
    {
        public Envelope ToEnvelope() => new(MinX, MaxX, MinY, MaxY);
    }
}
