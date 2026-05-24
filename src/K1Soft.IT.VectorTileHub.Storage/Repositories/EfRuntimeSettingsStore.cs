using Microsoft.EntityFrameworkCore;

namespace K1Soft.IT.VectorTileHub.Storage;

public sealed class EfRuntimeSettingsStore : IVectorTileRuntimeSettingsStore
{
    private readonly VectorTileHubDbContext _dbContext;

    public EfRuntimeSettingsStore(VectorTileHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VectorTileLayerRuntimeSettings?> GetLayerRuntimeSettingsAsync(int layerId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LayerRuntimeSettings.AsNoTracking().FirstOrDefaultAsync(x => x.LayerId == layerId, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task UpsertLayerRuntimeSettingsAsync(VectorTileLayerRuntimeSettings settings, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LayerRuntimeSettings.FirstOrDefaultAsync(x => x.LayerId == settings.LayerId, cancellationToken);
        if (entity is null)
        {
            entity = new LayerRuntimeSettingsEntity { LayerId = settings.LayerId };
            _dbContext.LayerRuntimeSettings.Add(entity);
        }

        entity.ActiveCacheVersion = settings.ActiveCacheVersion;
        entity.CacheGenerationStatus = settings.CacheGenerationStatus.ToString();
        entity.CacheGenerationJobId = settings.CacheGenerationJobId;
        entity.LastGenerationStartedAt = settings.LastGenerationStartedAt;
        entity.LastGenerationCompletedAt = settings.LastGenerationCompletedAt;
        entity.LastInvalidatedAt = settings.LastInvalidatedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Metadata = settings.Metadata;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VectorTileLayerRuntimeSettings>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _dbContext.LayerRuntimeSettings.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToArray();
    }

    private static VectorTileLayerRuntimeSettings ToModel(LayerRuntimeSettingsEntity entity)
    {
        return new VectorTileLayerRuntimeSettings
        {
            LayerId = entity.LayerId,
            ActiveCacheVersion = entity.ActiveCacheVersion,
            CacheGenerationStatus = Enum.TryParse<CacheGenerationStatus>(entity.CacheGenerationStatus, out var status) ? status : CacheGenerationStatus.Idle,
            CacheGenerationJobId = entity.CacheGenerationJobId,
            LastGenerationStartedAt = entity.LastGenerationStartedAt,
            LastGenerationCompletedAt = entity.LastGenerationCompletedAt,
            LastInvalidatedAt = entity.LastInvalidatedAt,
            UpdatedAt = entity.UpdatedAt,
            Metadata = entity.Metadata
        };
    }
}
