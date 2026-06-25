using K1Soft.IT.VectorTileHub;
using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace K1Soft.IT.VectorTileHub.Core.Tests;

/// <summary>
/// Regression tests for the concurrent first-insert race in
/// <see cref="EfRuntimeSettingsStore.UpsertLayerRuntimeSettingsAsync"/>.
///
/// On the first view of a newly-published layer the map fires many tile requests at once,
/// each of which lazily upserts the same LayerId. Against SQLite (where a write is not
/// visible to other connections until committed) the losing inserters used to exhaust a
/// one-shot retry and leak a UNIQUE-constraint <see cref="DbUpdateException"/>. The fix
/// retries a bounded number of times with a small backoff so a loser eventually sees the
/// winner's committed row and takes the UPDATE path.
/// </summary>
public sealed class EfRuntimeSettingsStoreConcurrencyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vth-upsert-race-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    private VectorTileHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VectorTileHubDbContext>()
            .UseSqlite(ConnectionString)
            .Options;
        return new VectorTileHubDbContext(options);
    }

    [Fact]
    public async Task UpsertLayerRuntimeSettingsAsync_ConcurrentFirstInserts_NoThrowAndSingleRow()
    {
        const int layerId = 42;
        const int concurrency = 8;

        // Materialise the schema once up front so the parallel writers only race on the row.
        await using (var setup = CreateContext())
        {
            await setup.Database.EnsureCreatedAsync();
        }

        // Each "request" gets its own DbContext (DbContext is not thread-safe), mirroring the
        // scoped lifetime in the host. They all upsert the SAME LayerId simultaneously.
        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            await using var context = CreateContext();
            var store = new EfRuntimeSettingsStore(context, new ServerSettingsMirror());
            await store.UpsertLayerRuntimeSettingsAsync(
                new VectorTileLayerRuntimeSettings
                {
                    LayerId = layerId,
                    ActiveCacheVersion = $"v{i}",
                    CacheGenerationStatus = CacheGenerationStatus.Idle
                },
                CancellationToken.None);
        });

        // No request may throw — the losing inserters must converge onto the UPDATE path.
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);

        // Exactly one row must exist for the layer, regardless of who won the race.
        await using var verify = CreateContext();
        var rows = await verify.LayerRuntimeSettings.CountAsync(x => x.LayerId == layerId);
        Assert.Equal(1, rows);
    }

    public void Dispose()
    {
        // Drop pooled connections to the file before deleting it.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
