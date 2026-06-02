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

    /// <summary>
    /// Filtered cache variants. Each rule yields an independently cached, separately
    /// addressable variant selected by its <see cref="CacheRuleConfig.VariantKey"/>.
    /// Empty = a single unfiltered "default" variant.
    /// </summary>
    public List<CacheRuleConfig> CacheRules { get; set; } = [];

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

public sealed class LayerCacheConfig
{
    public bool Enabled { get; set; } = true;
    public string? CacheRootFolder { get; set; }

    /// <summary>
    /// Tile age (minutes) after which a cached tile is considered stale and
    /// refreshed in the background (stale-while-revalidate). 0 = never stale.
    /// </summary>
    public int RefreshPeriodMinutes { get; set; }
}
