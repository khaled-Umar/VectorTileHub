namespace K1Soft.IT.VectorTileHub.Storage;

public sealed class LayerRuntimeSettingsEntity
{
    public int LayerId { get; set; }
    public string ActiveCacheVersion { get; set; } = "default";
    public string CacheGenerationStatus { get; set; } = "Idle";
    public string? CacheGenerationJobId { get; set; }
    public DateTimeOffset? LastGenerationStartedAt { get; set; }
    public DateTimeOffset? LastGenerationCompletedAt { get; set; }
    public DateTimeOffset? LastInvalidatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }
}
