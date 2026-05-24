using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K1Soft.IT.VectorTileHub.Storage.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LayerRuntimeSettings",
            columns: table => new
            {
                LayerId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ActiveCacheVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CacheGenerationStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CacheGenerationJobId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                LastGenerationStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastGenerationCompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastInvalidatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Metadata = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LayerRuntimeSettings", x => x.LayerId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LayerRuntimeSettings");
    }
}
