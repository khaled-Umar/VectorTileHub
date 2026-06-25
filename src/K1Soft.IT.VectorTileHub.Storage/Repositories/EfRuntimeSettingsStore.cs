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

    // Number of upsert attempts before a UNIQUE-violation is treated as a genuine failure
    // rather than a lost insert race. See the loop below for why more than one retry is needed.
    private const int MaxAttempts = 5;

    public async Task UpsertLayerRuntimeSettingsAsync(VectorTileLayerRuntimeSettings settings, CancellationToken cancellationToken)
    {
        // Concurrency-safe upsert of the single row keyed by LayerId. On the first view of a
        // newly-published layer the map fires many tile requests at once; each cache-miss lazily
        // calls this upsert, so 3+ requests race to INSERT the same LayerId. The losers fail the
        // INSERT with a UNIQUE violation and must retry as an UPDATE of the winner's row.
        //
        // Why a bounded *loop* and not a single retry: with SQLite, a write is not visible to other
        // connections until it is committed (cross-connection visibility lag). A losing inserter's
        // first retry re-SELECTs, but the winner may still not have committed, so the SELECT again
        // returns null and it INSERTs (and fails) a second time. A one-shot retry therefore leaks a
        // DbUpdateException under real concurrency. Looping a few times — with a tiny backoff so the
        // winning commit becomes visible — lets the loser eventually SELECT the committed row and
        // take the UPDATE path. Each iteration re-SELECTs at the top, so no extra query is needed.
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
            catch (DbUpdateException) when (isInsert && attempt < MaxAttempts - 1)
            {
                // Another request inserted this LayerId first. Drop our failed insert so the next
                // iteration starts clean, then wait briefly for the winner's commit to become
                // visible across connections before re-SELECTing. The backoff grows slightly with
                // each attempt; on the final attempt the guard above no longer matches, so a
                // genuine (non-race) failure still surfaces instead of being swallowed.
                _dbContext.Entry(entity).State = EntityState.Detached;
                await Task.Delay(5 * (attempt + 1), cancellationToken);
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
