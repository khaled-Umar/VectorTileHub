using Hangfire;

namespace K1Soft.IT.VectorTileHub.Jobs;

public sealed class CacheSwapJob
{
    private readonly IVectorTileRuntimeSettingsStore _settings;
    private readonly IBackgroundJobClient _jobs;
    private readonly Microsoft.Extensions.Options.IOptions<VectorTileHubOptions> _options;

    public CacheSwapJob(IVectorTileRuntimeSettingsStore settings, IBackgroundJobClient jobs, Microsoft.Extensions.Options.IOptions<VectorTileHubOptions> options)
    {
        _settings = settings;
        _jobs = jobs;
        _options = options;
    }

    public async Task Execute(int layerId, string newVersion, bool regenerateAfterSwap, bool deleteOldVersion, CancellationToken cancellationToken)
    {
        var runtime = await _settings.GetLayerRuntimeSettingsAsync(layerId, cancellationToken) ?? new VectorTileLayerRuntimeSettings { LayerId = layerId };
        if (runtime.CacheGenerationStatus == CacheGenerationStatus.Running)
        {
            throw new InvalidOperationException($"Layer {layerId} already has a running cache operation.");
        }

        var oldVersion = runtime.ActiveCacheVersion;
        Directory.CreateDirectory(Path.Combine(_options.Value.DefaultCacheRootFolder, layerId.ToString(), "public", newVersion));
        runtime.ActiveCacheVersion = newVersion;
        runtime.UpdatedAt = DateTimeOffset.UtcNow;
        await _settings.UpsertLayerRuntimeSettingsAsync(runtime, cancellationToken);

        if (regenerateAfterSwap)
        {
            _jobs.Enqueue<CacheGenerationJob>(job => job.Execute(layerId, null, null, null, CancellationToken.None));
        }

        if (deleteOldVersion && !string.IsNullOrWhiteSpace(oldVersion))
        {
            var deleteJobId = _jobs.Enqueue<CacheDeletionJob>(job => job.Execute(layerId, oldVersion, false, CancellationToken.None));
        }
    }
}
