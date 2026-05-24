using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace K1Soft.IT.VectorTileHub.Storage.Migrations;

[DbContext(typeof(VectorTileHubDbContext))]
partial class VectorTileHubDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
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
