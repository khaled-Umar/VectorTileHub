using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub.AspNetCore;

public sealed class VectorTileHubHealthCheck : IHealthCheck
{
    private readonly IVectorTileRuntimeSettingsStore _runtimeSettings;
    private readonly VectorTileHubOptions _options;

    public VectorTileHubHealthCheck(IVectorTileRuntimeSettingsStore runtimeSettings, IOptions<VectorTileHubOptions> options)
    {
        _runtimeSettings = runtimeSettings;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await _runtimeSettings.GetAllAsync(cancellationToken);
            checks["settingsStore"] = "Healthy";
        }
        catch
        {
            checks["settingsStore"] = "Unhealthy";
        }

        checks["cacheFolder"] = CheckWritableFolder(_options.DefaultCacheRootFolder) ? "Healthy" : "Unhealthy";
        checks["layerConfigFolder"] = Directory.Exists(_options.LayerConfigFolder) ? "Healthy" : "Unhealthy";

        var healthy = checks.Values.All(x => x == "Healthy");
        var data = checks.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase);
        return healthy
            ? HealthCheckResult.Healthy("VectorTileHub is healthy", data)
            : HealthCheckResult.Unhealthy("VectorTileHub is unhealthy", data: data);
    }

    private static bool CheckWritableFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, $".vth-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, "");
            File.Delete(temp);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
