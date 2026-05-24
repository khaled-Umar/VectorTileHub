# Implementation Plan: VectorTileHub — Reusable Vector Tile Engine

**Branch**: `001-vector-tile-engine` | **Date**: 2026-05-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-vector-tile-engine/spec.md`

## Summary

Build a reusable ASP.NET Core library (`K1Soft.IT.VectorTileHub`) that
serves secure, cache-aware, provider-agnostic Mapbox Vector Tiles
(MVT/PBF) from configurable data sources. The library exposes tile
endpoints, admin endpoints, layer metadata, health checks, and
background job management. SQL Server and Oracle providers are included
in the first version. A sample ASP.NET Core application demonstrates
realistic integration.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0 (LTS)

**Primary Dependencies**:
- NetTopologySuite 2.x — geometry model and spatial operations
- NetTopologySuite.IO.VectorTiles.Mapbox — MVT/PBF encoding
- Microsoft.Data.SqlClient — SQL Server ADO.NET provider
- Oracle.ManagedDataAccess.Core — Oracle ADO.NET provider
- Hangfire.Core + Hangfire.AspNetCore — background jobs
- Microsoft.EntityFrameworkCore.Sqlite — internal runtime settings
- Microsoft.Extensions.Caching.Memory — optional memory cache
- Microsoft.Extensions.Diagnostics.HealthChecks — health check

**Storage**:
- SQL Server (tile data provider)
- Oracle (tile data provider)
- SQLite (default internal runtime settings store)
- Disk filesystem (tile cache)
- In-memory (optional cache layer)

**Testing**: xUnit + Microsoft.AspNetCore.Mvc.Testing + NSubstitute

**Target Platform**: Windows / Linux server (ASP.NET Core cross-platform)

**Project Type**: Reusable NuGet library + sample web application

**Performance Goals**: <50ms cached tile response, 1,000+ concurrent
cached requests without degradation

**Constraints**: Server-side security enforcement, parameterized SQL
only, no EF Core on hot-path tile queries, attribute whitelisting
mandatory

**Scale/Scope**: Multi-layer, multi-provider, scope-aware caching,
single-server deployment for v1

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Library-First Rule | PASS | All core capabilities in library projects; sample app is only a consumer |
| II | Separation of Concerns | PASS | 7 projects with clear responsibility boundaries (see Project Structure) |
| III | Policy-Driven Request Handling | PASS | Tile orchestration implements the 7-step policy decision pipeline |
| IV | Security (NON-NEGOTIABLE) | PASS | Server-side scope resolution, parameterized SQL, whitelist-only attributes, admin auth |
| V | Provider Independence | PASS | Abstractions project defines contracts; providers are separate projects with no core knowledge |
| VI | Performance | PASS | ADO.NET for hot-path spatial queries; EF Core only for settings/metadata |
| VII | Configuration | PASS | appsettings.json + per-layer JSON files; all settings externally configurable |
| VIII | Tile Output | PASS | XYZ addressing, EPSG:3857 default, extent 4096, buffer 64, whitelist-only |
| IX | Cache | PASS | Cache key = layer + z/x/y + scope + version; disk + memory layers; safe two-stage replacement |
| X | SQL | PASS | Parameterized only; no user-controlled concatenation |
| XI | Runtime Settings | PASS | SQLite durable store by default; memory cache for fast lookup |
| XII | Sample Application | PASS | Separate project consuming library as external host would |
| XIII | First-Version Discipline | PASS | SQL Server + Oracle only; no distributed complexity; no overbuilt admin UI |
| XIV | Decision Order | PASS | Security > correctness > reusability > performance > extensibility |

**Gate result**: ALL PASS — no violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-vector-tile-engine/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── api-public.md
│   ├── api-admin.md
│   └── interfaces.md
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (repository root)

```text
src/
├── K1Soft.IT.VectorTileHub.Abstractions/
│   ├── Interfaces/
│   │   ├── IVectorTileService.cs
│   │   ├── IVectorTileFeatureProvider.cs
│   │   ├── IVectorTileEncoder.cs
│   │   ├── IVectorTileCache.cs
│   │   ├── IVectorTileSecurityScopeResolver.cs
│   │   ├── IVectorTileRuntimeSettingsStore.cs
│   │   └── IVectorTileLayerConfigProvider.cs
│   └── Models/
│       ├── VectorTileFeature.cs
│       ├── VectorTileFeatureBatch.cs
│       ├── VectorTileFeatureQuery.cs
│       ├── VectorTileResult.cs
│       ├── VectorTileCacheKey.cs
│       ├── VectorTileCacheOptions.cs
│       ├── VectorTileEncodingContext.cs
│       ├── VectorTileSecurityScope.cs
│       ├── VectorTileLayerConfig.cs
│       ├── VectorTileLayerRuntimeSettings.cs
│       └── VectorTileHubOptions.cs
│
├── K1Soft.IT.VectorTileHub.Core/
│   ├── Services/
│   │   ├── VectorTileOrchestrator.cs
│   │   ├── DiskTileCache.cs
│   │   ├── MemoryTileCache.cs
│   │   ├── CompositeTileCache.cs
│   │   ├── DefaultSecurityScopeResolver.cs
│   │   └── JsonLayerConfigProvider.cs
│   ├── Encoding/
│   │   └── MapboxVectorTileEncoder.cs
│   ├── TileMath/
│   │   └── TileCoordinateUtils.cs
│   └── DependencyInjection/
│       └── VectorTileHubCoreServiceCollectionExtensions.cs
│
├── K1Soft.IT.VectorTileHub.AspNetCore/
│   ├── Endpoints/
│   │   ├── TileEndpoints.cs
│   │   ├── LayerMetadataEndpoints.cs
│   │   ├── AdminCacheEndpoints.cs
│   │   └── AdminConfigEndpoints.cs
│   ├── HealthChecks/
│   │   └── VectorTileHubHealthCheck.cs
│   └── DependencyInjection/
│       ├── VectorTileHubServiceCollectionExtensions.cs
│       └── VectorTileHubEndpointRouteBuilderExtensions.cs
│
├── K1Soft.IT.VectorTileHub.Providers.SqlServer/
│   ├── SqlServerFeatureProvider.cs
│   └── DependencyInjection/
│       └── SqlServerProviderServiceCollectionExtensions.cs
│
├── K1Soft.IT.VectorTileHub.Providers.Oracle/
│   ├── OracleFeatureProvider.cs
│   └── DependencyInjection/
│       └── OracleProviderServiceCollectionExtensions.cs
│
├── K1Soft.IT.VectorTileHub.Storage/
│   ├── VectorTileHubDbContext.cs
│   ├── Entities/
│   │   └── LayerRuntimeSettingsEntity.cs
│   ├── Repositories/
│   │   └── EfRuntimeSettingsStore.cs
│   └── DependencyInjection/
│       └── StorageServiceCollectionExtensions.cs
│
├── K1Soft.IT.VectorTileHub.Jobs/
│   ├── CacheGenerationJob.cs
│   ├── CacheDeletionJob.cs
│   ├── CacheInvalidationJob.cs
│   ├── CacheSwapJob.cs
│   └── DependencyInjection/
│       └── JobsServiceCollectionExtensions.cs
│
└── K1Soft.IT.VectorTileHub.Sample/
    ├── Program.cs
    ├── appsettings.json
    └── VectorTileHub/
        └── Layers/
            └── 82-layer-data.json

tests/
├── K1Soft.IT.VectorTileHub.Core.Tests/
├── K1Soft.IT.VectorTileHub.AspNetCore.Tests/
└── K1Soft.IT.VectorTileHub.Integration.Tests/
```

**Structure Decision**: Multi-project .NET solution following the
constitution's separation of concerns. Abstractions defines all
contracts. Core implements orchestration, caching, encoding. AspNetCore
wires endpoints and DI. Providers are isolated per database engine.
Storage handles durable runtime settings. Jobs manages background
workflows. Sample consumes the library as an external host.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | —          | —                                   |
