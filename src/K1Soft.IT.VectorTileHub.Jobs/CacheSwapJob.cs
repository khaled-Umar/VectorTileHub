using Hangfire;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.Jobs;

/// <summary>
/// Blue/green cache replacement (job A): build into a new empty version folder and
/// flip the active version immediately; a separate <see cref="CacheDeletionJob"/>
/// (job B) removes the old version afterward, so cutover never waits on deletion.
/// </summary>
public sealed class CacheSwapJob
{
    private readonly IVectorTileRuntimeSettingsStore _settings;
    private readonly IBackgroundJobClient _jobs;
    private readonly IOptions<VectorTileHubOptions> _options;

    public CacheSwapJob(IVectorTileRuntimeSettingsStore settings, IBackgroundJobClient jobs, IOptions<VectorTileHubOptions> options)
    {
        _settings = settings;
        _jobs = jobs;
        _options = options;
    }

    public async Task Execute(int layerId, string newVersion, bool regenerateAfterSwap, bool deleteOldVersion, CancellationToken cancellationToken)
    {
        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };

        var oldVersion = runtime.ActiveCacheVersion;
        Directory.CreateDirectory(Path.Combine(_options.Value.DefaultCacheRootFolder, layerId.ToString(), newVersion));
        runtime.ActiveCacheVersion = newVersion;
        runtime.UpdatedAt = DateTimeOffset.UtcNow;
        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);

        if (regenerateAfterSwap)
        {
            _jobs.Enqueue<CacheGenerationJob>(job => job.Execute(layerId, null, null, null, CancellationToken.None));
        }

        if (deleteOldVersion && !string.IsNullOrWhiteSpace(oldVersion) && !string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase))
        {
            _jobs.Enqueue<CacheDeletionJob>(job => job.Execute(layerId, oldVersion, false, CancellationToken.None));
        }
    }
}
