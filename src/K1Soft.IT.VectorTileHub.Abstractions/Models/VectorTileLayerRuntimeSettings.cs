namespace K1Soft.IT.VectorTileHub;

public enum CacheGenerationStatus
{
    Idle,
    Running,
    Failed
}

public sealed class VectorTileLayerRuntimeSettings
{
    public int LayerId { get; set; }
    public string ActiveCacheVersion { get; set; } = "default";
    public CacheGenerationStatus CacheGenerationStatus { get; set; } = CacheGenerationStatus.Idle;
    public string? CacheGenerationJobId { get; set; }
    public DateTimeOffset? LastGenerationStartedAt { get; set; }
    public DateTimeOffset? LastGenerationCompletedAt { get; set; }
    public DateTimeOffset? LastInvalidatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }
}
