using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.EntityFrameworkCore;

namespace K1Soft.IT.VectorTileHub.Storage;

public sealed class VectorTileHubDbContext : DbContext
{
    public VectorTileHubDbContext(DbContextOptions<VectorTileHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<LayerRuntimeSettingsEntity> LayerRuntimeSettings => Set<LayerRuntimeSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LayerRuntimeSettingsEntity>(entity =>
        {
            entity.ToTable("LayerRuntimeSettings");
            entity.HasKey(x => x.LayerId);
            entity.Property(x => x.ActiveCacheVersion).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CacheGenerationStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CacheGenerationJobId).HasMaxLength(128);
        });
    }
}
