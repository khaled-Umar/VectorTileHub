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
    /// Optional geographic bounds of the layer's data. When set, tile requests for tiles
    /// outside these bounds short-circuit (no database query) and cache generation is limited
    /// to this area instead of the whole world. Omit to disable extent gating.
    /// </summary>
    public ExtentConfig? Extent { get; set; }

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

    /// <summary>
    /// Per-layer database command timeout, in seconds, applied to each feature query.
    /// Null = use the ADO.NET provider default (30s). 0 = no timeout (wait indefinitely).
    /// Useful for heavy low-zoom queries that exceed the default timeout.
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }
}

public sealed class ExtentConfig
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    /// <summary>Coordinate system of the extent. Supported: 4326 (lon/lat) or 3857 (Web Mercator).</summary>
    public int Srid { get; set; } = 4326;
}

public sealed class TileConfig
{
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; } = 21;

    /// <summary>
    /// Optional ceiling for cache <em>generation</em> only (serving still honours <see cref="MaxZoom"/>).
    /// High zooms dominate tile counts, so capping generation here keeps full-layer jobs tractable;
    /// zooms above the cap are produced on demand. Null = generate up to <see cref="MaxZoom"/>.
    /// </summary>
    public int? MaxGenerationZoom { get; set; }
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
