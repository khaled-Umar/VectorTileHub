using Microsoft.EntityFrameworkCore;

namespace K1Soft.IT.VectorTileHub.Storage;

public sealed class EfRuntimeSettingsStore : IVectorTileRuntimeSettingsStore
{
    private readonly VectorTileHubDbContext _dbContext;
    private readonly ServerSettingsMirror _mirror;

    public EfRuntimeSettingsStore(VectorTileHubDbContext dbContext, ServerSettingsMirror mirror)
    {
        _dbContext = dbContext;
        _mirror = mirror;
    }

    public async Task<VectorTileLayerRuntimeSettings?> GetLayerRuntimeSettingsAsync(int layerId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LayerRuntimeSettings.AsNoTracking().FirstOrDefaultAsync(x => x.LayerId == layerId, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task UpsertLayerRuntimeSettingsAsync(VectorTileLayerRuntimeSettings settings, CancellationToken cancellationToken)
    {
        // Concurrency-safe upsert with a one-shot retry: on first map load many concurrent tile
        // requests race to lazily create the same layer's row, so a losing INSERT (UNIQUE violation
        // on LayerId) is retried as an UPDATE of the row the winning request just committed.
        for (var attempt = 0; ; attempt++)
        {
            var entity = await _dbContext.LayerRuntimeSettings.FirstOrDefaultAsync(x => x.LayerId == settings.LayerId, cancellationToken);
            var isInsert = entity is null;
            if (isInsert)
            {
                entity = new LayerRuntimeSettingsEntity { LayerId = settings.LayerId };
                _dbContext.LayerRuntimeSettings.Add(entity);
            }

            entity!.ActiveCacheVersion = settings.ActiveCacheVersion;
            entity.CacheGenerationStatus = settings.CacheGenerationStatus.ToString();
            entity.CacheGenerationJobId = settings.CacheGenerationJobId;
            entity.LastGenerationStartedAt = settings.LastGenerationStartedAt;
            entity.LastGenerationCompletedAt = settings.LastGenerationCompletedAt;
            entity.LastInvalidatedAt = settings.LastInvalidatedAt;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.Metadata = settings.Metadata;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (isInsert && attempt == 0)
            {
                // Another request inserted this LayerId first; drop our failed insert and retry,
                // which now finds the existing row and updates it instead.
                _dbContext.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    public async Task<IReadOnlyList<VectorTileLayerRuntimeSettings>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _dbContext.LayerRuntimeSettings.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToArray();
    }

    // Global key/value settings — read from the in-memory mirror (fast), write-through to DB.
    public string? GetSetting(string key) => _mirror.Get(key);

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ServerSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (entity is null)
        {
            entity = new ServerSettingEntity { Key = key };
            _dbContext.ServerSettings.Add(entity);
        }

        entity.Value = value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _mirror.Set(key, value);
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
