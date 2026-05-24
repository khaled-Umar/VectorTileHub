namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileHubOptions
{
    public bool Enabled { get; set; } = true;
    public string RoutePrefix { get; set; } = "/vector-tile-hub";
    public int DefaultServingSrid { get; set; } = 3857;
    public int DefaultTileExtent { get; set; } = 4096;
    public int DefaultTileBuffer { get; set; } = 64;
    public string LayerConfigFolder { get; set; } = "VectorTileHub/Layers";
    public string DefaultCacheRootFolder { get; set; } = "VectorTileHub/Cache";
    public bool UseResponseCompression { get; set; } = true;
    public bool UseMemoryCache { get; set; } = true;
    public bool UseDiskCache { get; set; } = true;
    public bool UseRedisCache { get; set; }
    public bool DefaultAuthenticationRequired { get; set; } = true;
    public string HealthCheckPath { get; set; } = "/vector-tile-hub/health";
    public SettingsStoreOptions InternalSettingsStore { get; set; } = new();
    public HangfireOptions Hangfire { get; set; } = new();
}

public sealed class SettingsStoreOptions
{
    public string Provider { get; set; } = "Sqlite";
    public string ConnectionString { get; set; } = "Data Source=VectorTileHub/vector_tile_hub.db";
}

public sealed class HangfireOptions
{
    public bool Enabled { get; set; } = true;
    public string DashboardPath { get; set; } = "/vector-tile-hub/jobs";
    public string[] RequiredRoles { get; set; } = ["Admin"];
}
