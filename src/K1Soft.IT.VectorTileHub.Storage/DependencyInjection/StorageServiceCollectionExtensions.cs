using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K1Soft.IT.VectorTileHub.Storage;

public static class StorageServiceCollectionExtensions
{
    public const string ActiveCacheRootPathKey = "ActiveCacheRootPath";

    public static IServiceCollection AddVectorTileHubStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("VectorTileHub").Get<VectorTileHubOptions>() ?? new VectorTileHubOptions();

        // Host-supplied connection, or auto-create a local SQLite store.
        var connectionString = string.IsNullOrWhiteSpace(options.InternalSettingsStore.ConnectionString)
            ? "Data Source=VectorTileHub/vector_tile_hub.db"
            : options.InternalSettingsStore.ConnectionString;

        services.AddDbContext<VectorTileHubDbContext>(builder => builder.UseSqlite(connectionString));
        services.AddSingleton<ServerSettingsMirror>();
        services.AddScoped<IVectorTileRuntimeSettingsStore, EfRuntimeSettingsStore>();
        return services;
    }

    public static async Task EnsureVectorTileHubStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VectorTileHubDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        // Warm the in-memory settings mirror from the durable store.
        var mirror = scope.ServiceProvider.GetRequiredService<ServerSettingsMirror>();
        var settings = await db.ServerSettings.AsNoTracking().ToListAsync(cancellationToken);
        mirror.LoadAll(settings.Select(s => new KeyValuePair<string, string>(s.Key, s.Value)));
    }
}
