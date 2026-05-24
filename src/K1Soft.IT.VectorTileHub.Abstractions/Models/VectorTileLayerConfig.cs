namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileLayerConfig
{
    public int Id { get; set; }
    public string LayerKey { get; set; } = "";
    public string LayerName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public ProviderConfig Provider { get; set; } = new();
    public TileConfig Tile { get; set; } = new();
    public AttributeConfig Attributes { get; set; } = new();
    public SecurityConfig? Security { get; set; }
    public LayerCacheConfig Cache { get; set; } = new();
}

public sealed class ProviderConfig
{
    public string Type { get; set; } = "";
    public string? ConnectionStringName { get; set; }
    public string? ConnectionString { get; set; }
    public string TableName { get; set; } = "";
    public string IdColumn { get; set; } = "Id";
    public string GeometryColumn { get; set; } = "Geom";
    public int SourceSrid { get; set; } = 3857;
    public string? CustomFilter { get; set; }
}

public sealed class TileConfig
{
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; } = 21;
    public int Extent { get; set; }
    public int Buffer { get; set; }
    public bool ClipGeometry { get; set; } = true;
    public bool ReturnEmptyTileOutsideZoomRange { get; set; } = true;
    public bool AllowOnDemandGeneration { get; set; } = true;
    public int ServingSrid { get; set; }
}

public sealed class AttributeConfig
{
    public string[] Include { get; set; } = [];
}

public sealed class SecurityConfig
{
    public bool? RequireAuthentication { get; set; }
    public string? ScopeColumn { get; set; }
    public Dictionary<string, string[]> ScopeMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LayerCacheConfig
{
    public bool Enabled { get; set; } = true;
    public string? CacheRootFolder { get; set; }
    public int TtlMinutes { get; set; }
}
