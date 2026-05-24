using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.Jobs;

public sealed class CacheDeletionJob
{
    private readonly VectorTileHubOptions _options;

    public CacheDeletionJob(IOptions<VectorTileHubOptions> options)
    {
        _options = options.Value;
    }

    public Task Execute(int layerId, string? cacheVersion, bool deleteAllVersions, CancellationToken cancellationToken)
    {
        var layerFolder = Path.Combine(_options.DefaultCacheRootFolder, layerId.ToString());
        if (deleteAllVersions)
        {
            DeleteIfExists(layerFolder);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(cacheVersion) && Directory.Exists(layerFolder))
        {
            foreach (var versionFolder in Directory.EnumerateDirectories(layerFolder, cacheVersion, SearchOption.AllDirectories))
            {
                DeleteIfExists(versionFolder);
            }
        }

        return Task.CompletedTask;
    }

    private static void DeleteIfExists(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
