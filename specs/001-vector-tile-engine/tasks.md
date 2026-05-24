# Tasks: VectorTileHub — Reusable Vector Tile Engine

**Input**: Design documents from `specs/001-vector-tile-engine/`

**Prerequisites**: plan.md (required), spec.md (required), research.md,
data-model.md, contracts/interfaces.md, contracts/api-public.md,
contracts/api-admin.md, quickstart.md

**Tests**: Not explicitly requested in the spec. Test tasks are omitted.

**Context**: Tasks are designed for implementation with Codex. Each task
includes exact file paths and references the design documents for
contract details.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US7)
- Reference `specs/001-vector-tile-engine/data-model.md` for all model field definitions
- Reference `specs/001-vector-tile-engine/contracts/interfaces.md` for all interface signatures
- Reference `specs/001-vector-tile-engine/contracts/api-public.md` for public endpoint contracts
- Reference `specs/001-vector-tile-engine/contracts/api-admin.md` for admin endpoint contracts

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create .NET solution structure with all projects and dependencies

- [X] T001 Create .NET 8.0 solution file `VectorTileHub.sln` at repository root with the following projects: `src/K1Soft.IT.VectorTileHub.Abstractions/K1Soft.IT.VectorTileHub.Abstractions.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Core/K1Soft.IT.VectorTileHub.Core.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.AspNetCore/K1Soft.IT.VectorTileHub.AspNetCore.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/K1Soft.IT.VectorTileHub.Providers.SqlServer.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Providers.Oracle/K1Soft.IT.VectorTileHub.Providers.Oracle.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Storage/K1Soft.IT.VectorTileHub.Storage.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Jobs/K1Soft.IT.VectorTileHub.Jobs.csproj` (classlib), `src/K1Soft.IT.VectorTileHub.Sample/K1Soft.IT.VectorTileHub.Sample.csproj` (web). All projects use `<RootNamespace>K1Soft.IT.VectorTileHub.*</RootNamespace>` and target `net8.0`. Set up project references: Core → Abstractions, AspNetCore → Core, Providers.SqlServer → Abstractions, Providers.Oracle → Abstractions, Storage → Abstractions, Jobs → Core + Storage, Sample → AspNetCore + Providers.SqlServer + Storage + Jobs

- [X] T002 Add NuGet package dependencies to each project: Abstractions → `NetTopologySuite (2.*)`, `System.Security.Claims`; Core → `NetTopologySuite.IO.VectorTiles.Mapbox`, `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`; AspNetCore → `Microsoft.Extensions.Diagnostics.HealthChecks`; Providers.SqlServer → `Microsoft.Data.SqlClient`, `NetTopologySuite`; Providers.Oracle → `Oracle.ManagedDataAccess.Core`, `NetTopologySuite`; Storage → `Microsoft.EntityFrameworkCore.Sqlite`; Jobs → `Hangfire.Core`, `Hangfire.AspNetCore`; Sample → `Hangfire.AspNetCore`

- [X] T003 [P] Create `.gitignore` for .NET projects at repository root (bin/, obj/, *.user, .vs/, *.db) and `.editorconfig` with C# conventions (indent_style=space, indent_size=4, namespace=file_scoped)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, interfaces, and shared infrastructure that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create all model classes in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`. Create one file per model as defined in `specs/001-vector-tile-engine/data-model.md`: `VectorTileHubOptions.cs` (with nested `SettingsStoreOptions` and `HangfireOptions`), `VectorTileLayerConfig.cs` (with nested `ProviderConfig`, `TileConfig`, `AttributeConfig`, `SecurityConfig`, `LayerCacheConfig`), `VectorTileFeature.cs`, `VectorTileFeatureBatch.cs`, `VectorTileFeatureQuery.cs`, `VectorTileResult.cs`, `VectorTileCacheKey.cs` (include `ToStringKey()` and `ToDiskPath(string cacheRoot)` methods), `VectorTileCacheOptions.cs`, `VectorTileEncodingContext.cs`, `VectorTileSecurityScope.cs`, `VectorTileLayerRuntimeSettings.cs` (with `CacheGenerationStatus` enum: Idle, Running, Failed). All classes use `namespace K1Soft.IT.VectorTileHub`

- [X] T005 [P] Create all interface files in `src/K1Soft.IT.VectorTileHub.Abstractions/Interfaces/`. Create one file per interface as defined in `specs/001-vector-tile-engine/contracts/interfaces.md`: `IVectorTileService.cs`, `IVectorTileFeatureProvider.cs` (with `string ProviderType` property), `IVectorTileEncoder.cs` (with `Encode` and `EncodeEmpty` methods), `IVectorTileCache.cs` (with `GetAsync`, `SetAsync`, `RemoveAsync`, `RemoveByEnvelopeAsync`), `IVectorTileSecurityScopeResolver.cs`, `IVectorTileRuntimeSettingsStore.cs` (with `GetLayerRuntimeSettingsAsync`, `UpsertLayerRuntimeSettingsAsync`, `GetAllAsync`), `IVectorTileLayerConfigProvider.cs` (with `GetLayer`, `GetLayerByKey`, `GetAllLayers`, `ReloadAsync`). All interfaces use `namespace K1Soft.IT.VectorTileHub`

- [X] T006 [P] Implement `TileCoordinateUtils` static class in `src/K1Soft.IT.VectorTileHub.Core/TileMath/TileCoordinateUtils.cs`. Methods: `GetTileEnvelope(int z, int x, int y)` returns NTS `Envelope` in EPSG:3857, `ExpandEnvelope(Envelope env, int buffer, int extent)` expands by buffer/extent ratio, `GetAffectedTiles(Envelope bbox, int zoom)` returns `IEnumerable<(int z, int x, int y)>` of tiles intersecting the bbox at the given zoom, `GetAffectedTilesForZoomRange(Envelope bbox, int minZoom, int maxZoom)` returns tiles across all zooms, `IsValidTile(int z, int x, int y)` validates tile coordinates are within mathematical bounds. Use standard Slippy Map formulas (see `specs/001-vector-tile-engine/research.md` R3)

- [X] T007 Implement `JsonLayerConfigProvider` in `src/K1Soft.IT.VectorTileHub.Core/Services/JsonLayerConfigProvider.cs`. Implements `IVectorTileLayerConfigProvider`. Constructor takes `IOptions<VectorTileHubOptions>` and `ILogger<JsonLayerConfigProvider>`. On construction, loads all `*.json` files from `LayerConfigFolder`, deserializes each to `VectorTileLayerConfig`, stores in a `ConcurrentDictionary<int, VectorTileLayerConfig>` keyed by `Id`. Applies global defaults (extent, buffer, srid) to layers that don't override. `ReloadAsync` re-reads all files from disk and replaces the dictionary atomically using `Interlocked.Exchange`. Invalid JSON files are logged as warnings and skipped. Thread-safe for concurrent reads during reload

- [X] T008 [P] Create `LayerRuntimeSettingsEntity.cs` in `src/K1Soft.IT.VectorTileHub.Storage/Entities/` and `VectorTileHubDbContext.cs` in `src/K1Soft.IT.VectorTileHub.Storage/`. The entity maps the `LayerRuntimeSettings` table from `specs/001-vector-tile-engine/research.md` R6. DbContext uses SQLite, configures `LayerId` as primary key. Include an initial EF Core migration

- [X] T009 Implement `EfRuntimeSettingsStore` in `src/K1Soft.IT.VectorTileHub.Storage/Repositories/EfRuntimeSettingsStore.cs`. Implements `IVectorTileRuntimeSettingsStore`. Uses `VectorTileHubDbContext` for CRUD. `UpsertLayerRuntimeSettingsAsync` creates or updates the entity. Maps between `VectorTileLayerRuntimeSettings` model and `LayerRuntimeSettingsEntity`. Sets `UpdatedAt` on every write

- [X] T010 [P] Implement `VectorTileHubCoreServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.Core/DependencyInjection/VectorTileHubCoreServiceCollectionExtensions.cs`. Extension method `AddVectorTileHubCore(this IServiceCollection services, IConfiguration configuration)` that: binds `VectorTileHubOptions` from config section `"VectorTileHub"`, registers `JsonLayerConfigProvider` as singleton `IVectorTileLayerConfigProvider`, registers `MapboxVectorTileEncoder` as singleton `IVectorTileEncoder`, registers `DefaultSecurityScopeResolver` as scoped `IVectorTileSecurityScopeResolver`, registers `VectorTileOrchestrator` as scoped `IVectorTileService`

- [X] T011 [P] Implement `StorageServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.Storage/DependencyInjection/StorageServiceCollectionExtensions.cs`. Extension method `AddVectorTileHubStorage(this IServiceCollection services, IConfiguration configuration)` that: reads `InternalSettingsStore` from options, registers `VectorTileHubDbContext` with SQLite connection string, registers `EfRuntimeSettingsStore` as scoped `IVectorTileRuntimeSettingsStore`, ensures database is created on startup

**Checkpoint**: Foundation ready — all models, interfaces, tile math, config loading, and storage are in place. User story implementation can begin.

---

## Phase 3: User Story 1 — Serve Vector Tiles from a Data Source (Priority: P1) MVP

**Goal**: A host developer configures one SQL Server layer and serves MVT/PBF tiles through the tile endpoint

**Independent Test**: Request `GET /vector-tile-hub/tiles/82/14/9978/7171.pbf` and verify a valid MVT response with only whitelisted attributes

### Implementation for User Story 1

- [ ] T012 [US1] Implement `MapboxVectorTileEncoder` in `src/K1Soft.IT.VectorTileHub.Core/Encoding/MapboxVectorTileEncoder.cs`. Implements `IVectorTileEncoder`. `Encode` method: accepts layer name, features list, and encoding context. Uses `NetTopologySuite.IO.VectorTiles.Mapbox` to create a VectorTile with the given layer name and extent. For each `VectorTileFeature`, adds geometry and whitelisted attributes. Returns PBF byte array. `EncodeEmpty` method: creates a valid MVT with zero features and returns PBF bytes. Respects `ClipGeometry` and `Buffer` from context. See `specs/001-vector-tile-engine/contracts/interfaces.md` for the full interface contract

- [X] T013 [US1] Implement `SqlServerFeatureProvider` in `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/SqlServerFeatureProvider.cs`. Implements `IVectorTileFeatureProvider` with `ProviderType = "SqlServer"`. `GetFeaturesAsync` method: opens ADO.NET connection using the layer's connection string, builds a parameterized SQL query `SELECT [IdColumn], [GeometryColumn].STAsBinary() AS GeomWkb, [attr1], [attr2]... FROM [Table] WHERE [GeometryColumn].STIntersects(geometry::STGeomFromWKB(@envelope, @srid)) = 1`. Attributes come from `query.LayerConfig.Attributes.Include`. Envelope parameter is the WKB of the query bounding box. Reads results with `SqlDataReader`, parses WKB geometry using NTS `WKBReader`, constructs `VectorTileFeature` for each row. Returns `VectorTileFeatureBatch`. All SQL is parameterized — NO string concatenation of user input. See `specs/001-vector-tile-engine/research.md` R2 for the SQL pattern

- [X] T014 [US1] Implement `SqlServerProviderServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/DependencyInjection/SqlServerProviderServiceCollectionExtensions.cs`. Extension method `AddVectorTileHubSqlServerProvider(this IServiceCollection services)` that registers `SqlServerFeatureProvider` as a keyed singleton `IVectorTileFeatureProvider` with key `"SqlServer"`

- [X] T015 [US1] Implement `VectorTileOrchestrator` in `src/K1Soft.IT.VectorTileHub.Core/Services/VectorTileOrchestrator.cs`. Implements `IVectorTileService`. Constructor injects `IVectorTileLayerConfigProvider`, `IVectorTileSecurityScopeResolver`, `IVectorTileCache` (nullable/optional), `IVectorTileEncoder`, `IVectorTileRuntimeSettingsStore`, `IServiceProvider` (for keyed provider resolution), `ILogger<VectorTileOrchestrator>`. `GetTileAsync` method implements the policy-driven pipeline: (1) look up layer by ID, return 404 result if not found or disabled, (2) validate z/x/y with `TileCoordinateUtils.IsValidTile`, return 400 if invalid, (3) check zoom range — if outside and `ReturnEmptyTileOutsideZoomRange`, return empty tile, (4) resolve security scope via `IVectorTileSecurityScopeResolver`, (5) get runtime settings for cache version, (6) build `VectorTileCacheKey`, (7) check cache if available, (8) on miss: resolve provider by `layer.Provider.Type` from DI keyed services, compute tile envelope with buffer via `TileCoordinateUtils`, build `VectorTileFeatureQuery`, call provider, encode features, write to cache, (9) return `VectorTileResult`. Log tile request details (layer, z/x/y, scope, cache hit/miss, duration) using structured logging

- [X] T016 [US1] Implement `TileEndpoints` in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/TileEndpoints.cs`. Static class with `MapTileEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)` method. Maps `GET {RoutePrefix}/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf`. Handler resolves `IVectorTileService` from DI, calls `GetTileAsync` with route parameters and `HttpContext.User`. Maps `VectorTileResult` to HTTP response: 200 with `application/x-protobuf` content type for success, 400/401/403/404/503 based on result status. See `specs/001-vector-tile-engine/contracts/api-public.md` for response contract

- [X] T017 [US1] Implement `VectorTileHubServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.AspNetCore/DependencyInjection/VectorTileHubServiceCollectionExtensions.cs`. Extension method `AddVectorTileHub(this IServiceCollection services, IConfiguration configuration)` that calls `AddVectorTileHubCore`, `AddVectorTileHubStorage`, registers cache services based on options (`UseMemoryCache`, `UseDiskCache`), and adds health checks

- [X] T018 [US1] Implement `VectorTileHubEndpointRouteBuilderExtensions` in `src/K1Soft.IT.VectorTileHub.AspNetCore/DependencyInjection/VectorTileHubEndpointRouteBuilderExtensions.cs`. Extension method `MapVectorTileHubEndpoints(this IEndpointRouteBuilder endpoints)` that reads `VectorTileHubOptions` from DI, calls `TileEndpoints.MapTileEndpoints`, `LayerMetadataEndpoints.MapLayerMetadataEndpoints`, `AdminCacheEndpoints.MapAdminCacheEndpoints`, `AdminConfigEndpoints.MapAdminConfigEndpoints`, and maps the health check endpoint. Also `UseVectorTileHubHangfireDashboard(this IApplicationBuilder app)` for dashboard

**Checkpoint**: User Story 1 complete — tiles can be served from SQL Server through the endpoint with attribute whitelisting. Core MVP is functional.

---

## Phase 4: User Story 2 — Secure Scope-Aware Tile Serving (Priority: P2)

**Goal**: Tiles enforce server-side security based on user identity and scope rules

**Independent Test**: Request same tile as two users with different scopes, verify different features returned

### Implementation for User Story 2

- [X] T019 [US2] Implement `DefaultSecurityScopeResolver` in `src/K1Soft.IT.VectorTileHub.Core/Services/DefaultSecurityScopeResolver.cs`. Implements `IVectorTileSecurityScopeResolver`. `ResolveAsync` method: (1) If layer has no `SecurityConfig` and `DefaultAuthenticationRequired` is false → return public scope with `ScopeKey = "public"`, (2) If layer has no `SecurityConfig` and `DefaultAuthenticationRequired` is true → require authenticated principal, return 401 scope if anonymous, else return scope from first matching role, (3) If layer has `SecurityConfig` → check `RequireAuthentication`, match user roles against `ScopeMappings` to determine `ScopeKey` and `FilterValues`, (4) If `scopeOverride` is provided, validate it against user's allowed scopes before accepting. Returns `VectorTileSecurityScope` with the resolved `ScopeKey`, `IsAuthenticated`, and `FilterValues`

- [X] T020 [US2] Add scope-based WHERE clause filtering to `SqlServerFeatureProvider` in `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/SqlServerFeatureProvider.cs`. When `query.SecurityScope.FilterValues` is non-null and `query.LayerConfig.Security.ScopeColumn` is set, append `AND [ScopeColumn] IN (@scope0, @scope1, ...)` to the SQL query using parameterized values. The scope filter is always server-side and parameterized — NEVER concatenated

- [X] T021 [US2] Update `TileEndpoints` in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/TileEndpoints.cs` to: read optional `?scope=` query parameter and pass to `GetTileAsync` as `scopeOverride`, handle 401 and 403 result statuses from the orchestrator, ensure `HttpContext.User` (ClaimsPrincipal) is always passed to the service

- [X] T022 [US2] Update `VectorTileOrchestrator` in `src/K1Soft.IT.VectorTileHub.Core/Services/VectorTileOrchestrator.cs` to: check `scope.IsAuthenticated` against layer auth requirements, return 401 result if auth required but user is anonymous, return 403 if authenticated but no matching scope, include `ScopeKey` in structured log entries

**Checkpoint**: User Story 2 complete — tiles are scope-filtered server-side. Different scopes see different data. Unauthenticated requests to protected layers are rejected.

---

## Phase 5: User Story 3 — Cache-Aware Tile Serving (Priority: P3)

**Goal**: Generated tiles are cached to disk and optionally memory, reducing database load

**Independent Test**: Request same tile twice, verify second request returns from cache without provider query

### Implementation for User Story 3

- [X] T023 [P] [US3] Implement `DiskTileCache` in `src/K1Soft.IT.VectorTileHub.Core/Services/DiskTileCache.cs`. Implements `IVectorTileCache`. Uses `VectorTileCacheKey.ToDiskPath(cacheRoot)` for file paths. `GetAsync`: check if file exists, read bytes, return. `SetAsync`: create directory structure, write bytes to file. Wrap I/O in try-catch — log and skip on failure (disk full, permission denied), never throw. `RemoveAsync`: delete file if exists. `RemoveByEnvelopeAsync`: use `TileCoordinateUtils.GetAffectedTilesForZoomRange` to compute tile coordinates, delete matching files

- [X] T024 [P] [US3] Implement `MemoryTileCache` in `src/K1Soft.IT.VectorTileHub.Core/Services/MemoryTileCache.cs`. Implements `IVectorTileCache`. Uses `IMemoryCache` from `Microsoft.Extensions.Caching.Memory`. Cache key is `VectorTileCacheKey.ToStringKey()`. `SetAsync` uses `MemoryCacheEntryOptions` with sliding expiration from `VectorTileCacheOptions.TtlMinutes` (if > 0). `RemoveByEnvelopeAsync` is a no-op for memory cache (memory entries expire naturally)

- [X] T025 [US3] Implement `CompositeTileCache` in `src/K1Soft.IT.VectorTileHub.Core/Services/CompositeTileCache.cs`. Implements `IVectorTileCache`. Wraps an optional `MemoryTileCache` and optional `DiskTileCache`. `GetAsync`: check memory first, then disk. If found in disk but not memory, promote to memory. `SetAsync`: write to both. `RemoveAsync` and `RemoveByEnvelopeAsync`: remove from both. Constructor accepts `VectorTileHubOptions` to determine which layers are active

- [X] T026 [US3] Update `VectorTileOrchestrator` in `src/K1Soft.IT.VectorTileHub.Core/Services/VectorTileOrchestrator.cs` to integrate cache. After building `VectorTileCacheKey`: (1) call `_cache.GetAsync(key)`, (2) if hit, return `VectorTileResult` with `FromCache = true`, (3) if miss, generate tile via provider + encoder, (4) call `_cache.SetAsync(key, tileBytes, options)`, (5) return result with `FromCache = false`. Log cache hit/miss in structured log entry

- [X] T027 [US3] Update `VectorTileHubServiceCollectionExtensions` to register cache services. If `UseMemoryCache` is true, register `MemoryTileCache`. If `UseDiskCache` is true, register `DiskTileCache`. Always register `CompositeTileCache` as the `IVectorTileCache` implementation, injecting the enabled sub-caches

**Checkpoint**: User Story 3 complete — tiles are cached to disk and/or memory. Repeat requests are served from cache with scope-aware keys.

---

## Phase 6: User Story 4 — Background Cache Management (Priority: P4)

**Goal**: Admin endpoints trigger background cache generation, invalidation, deletion, and safe swap

**Independent Test**: POST to cache generation endpoint, verify job runs in background and tiles appear in cache folder

### Implementation for User Story 4

- [X] T028 [P] [US4] Implement `CacheGenerationJob` in `src/K1Soft.IT.VectorTileHub.Jobs/CacheGenerationJob.cs`. Constructor injects `IVectorTileLayerConfigProvider`, `IVectorTileFeatureProvider` (via `IServiceProvider` for keyed resolution), `IVectorTileEncoder`, `IVectorTileCache`, `IVectorTileRuntimeSettingsStore`, `ILogger`. `Execute(int layerId, int? minZoom, int? maxZoom, string[]? scopes)` method: (1) update runtime settings to `CacheGenerationStatus.Running`, (2) compute all tile coordinates for the layer's zoom range using `TileCoordinateUtils`, (3) for each tile + scope combination: query provider, encode, write to cache, (4) on completion: set status to `Idle`, update `LastGenerationCompletedAt`, (5) on exception: set status to `Failed`, log error, do NOT delete partial tiles. Use `CancellationToken` for graceful shutdown

- [X] T029 [P] [US4] Implement `CacheDeletionJob` in `src/K1Soft.IT.VectorTileHub.Jobs/CacheDeletionJob.cs`. `Execute(int layerId, string? cacheVersion, bool deleteAllVersions)` method: if `deleteAllVersions`, delete entire layer cache folder. If `cacheVersion` specified, delete only that version subfolder. Log deletion progress

- [X] T030 [P] [US4] Implement `CacheInvalidationJob` in `src/K1Soft.IT.VectorTileHub.Jobs/CacheInvalidationJob.cs`. `Execute(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? scopes)` method: create `Envelope` from bounding box, use `TileCoordinateUtils.GetAffectedTilesForZoomRange` to compute affected tiles, call `IVectorTileCache.RemoveAsync` for each tile+scope combination, update `LastInvalidatedAt` in runtime settings, return count of tiles invalidated

- [X] T031 [US4] Implement `CacheSwapJob` in `src/K1Soft.IT.VectorTileHub.Jobs/CacheSwapJob.cs`. `Execute(int layerId, string newVersion, bool regenerateAfterSwap, bool deleteOldVersion)` method: (1) read current `ActiveCacheVersion`, (2) create new cache folder, (3) update runtime settings with `ActiveCacheVersion = newVersion`, (4) if `regenerateAfterSwap`, enqueue `CacheGenerationJob` via `IBackgroundJobClient`, (5) if `deleteOldVersion`, enqueue `CacheDeletionJob` for old version as a continuation. Reject if a swap is already in progress (status == Running)

- [X] T032 [US4] Implement `AdminCacheEndpoints` in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/AdminCacheEndpoints.cs`. Static class with `MapAdminCacheEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)`. Maps all 6 admin cache endpoints per `specs/001-vector-tile-engine/contracts/api-admin.md`: generate, delete, invalidate, notify-change, swap, status. All endpoints require authorization via `.RequireAuthorization()`. Generate/delete/invalidate/swap enqueue Hangfire jobs via `IBackgroundJobClient` and return 202 with job ID. Status reads from `IVectorTileRuntimeSettingsStore`. Notify-change computes affected tiles from bounding box and invalidates + optionally enqueues regeneration

- [X] T033 [US4] Implement `JobsServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.Jobs/DependencyInjection/JobsServiceCollectionExtensions.cs`. Extension method `AddVectorTileHubJobs(this IServiceCollection services, IConfiguration configuration)` that registers all job classes as transient. Also `UseVectorTileHubHangfireDashboard(this IApplicationBuilder app)` that mounts the Hangfire dashboard at the configured `DashboardPath` with authorization filter requiring the configured `RequiredRoles`

**Checkpoint**: User Story 4 complete — operators can trigger cache generation, invalidation, deletion, and safe swap via admin endpoints. Jobs run in background without blocking tile serving.

---

## Phase 7: User Story 5 — Multi-Provider Layer Configuration (Priority: P5)

**Goal**: Add Oracle provider so layers can use different database engines through the same abstraction

**Independent Test**: Configure two layers (SQL Server + Oracle), request tiles from each, verify both return valid MVT

### Implementation for User Story 5

- [X] T034 [US5] Implement `OracleFeatureProvider` in `src/K1Soft.IT.VectorTileHub.Providers.Oracle/OracleFeatureProvider.cs`. Implements `IVectorTileFeatureProvider` with `ProviderType = "Oracle"`. Same contract as SqlServerFeatureProvider but uses Oracle SQL dialect: `SELECT "ID_COLUMN", SDO_UTIL.TO_WKBGEOMETRY("GEOM") AS GEOM_WKB, "ATTR1"... FROM "SCHEMA"."TABLE" WHERE SDO_RELATE("GEOM", SDO_GEOMETRY(2003, :srid, NULL, SDO_ELEM_INFO_ARRAY(1, 1003, 3), SDO_ORDINATE_ARRAY(:minx, :miny, :maxx, :maxy)), 'mask=ANYINTERACT') = 'TRUE'`. Uses `OracleConnection` and `OracleCommand` from `Oracle.ManagedDataAccess.Core`. All SQL parameterized. Parses WKB geometry with NTS `WKBReader`. Adds scope-based `AND` clause when `SecurityScope.FilterValues` is set. See `specs/001-vector-tile-engine/research.md` R2 for Oracle SQL pattern

- [X] T035 [US5] Implement `OracleProviderServiceCollectionExtensions` in `src/K1Soft.IT.VectorTileHub.Providers.Oracle/DependencyInjection/OracleProviderServiceCollectionExtensions.cs`. Extension method `AddVectorTileHubOracleProvider(this IServiceCollection services)` that registers `OracleFeatureProvider` as a keyed singleton `IVectorTileFeatureProvider` with key `"Oracle"`

- [X] T036 [US5] Verify keyed provider resolution in `VectorTileOrchestrator`. The orchestrator already resolves providers by `layer.Provider.Type` key from DI. Verify that when a layer config has `"type": "Oracle"`, the `OracleFeatureProvider` is resolved, and when `"type": "SqlServer"`, the `SqlServerFeatureProvider` is resolved. Add a guard: if no provider is registered for the layer's type, return a 503 result with a descriptive error log

**Checkpoint**: User Story 5 complete — layers can use SQL Server or Oracle providers interchangeably. New providers can be added by implementing `IVectorTileFeatureProvider` and registering with a keyed service.

---

## Phase 8: User Story 6 — Layer Metadata and Discovery (Priority: P6)

**Goal**: Public endpoints expose frontend-safe layer metadata

**Independent Test**: Request `GET /vector-tile-hub/layers`, verify response lists enabled layers with safe metadata only

### Implementation for User Story 6

- [X] T037 [US6] Implement `LayerMetadataEndpoints` in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/LayerMetadataEndpoints.cs`. Static class with `MapLayerMetadataEndpoints(this IEndpointRouteBuilder endpoints, VectorTileHubOptions options)`. Maps two endpoints per `specs/001-vector-tile-engine/contracts/api-public.md`: `GET {RoutePrefix}/layers` returns JSON array of enabled layers with only safe fields (id, layerKey, layerName, minZoom, maxZoom, tileUrlTemplate). `GET {RoutePrefix}/layers/{layerId:int}` returns single layer or 404. Create a `LayerMetadataDto` record with only the safe fields — MUST NOT include connection strings, provider config, security rules, or internal settings

**Checkpoint**: User Story 6 complete — frontends can discover available layers and build tile URLs dynamically.

---

## Phase 9: User Story 7 — Sample Application Integration (Priority: P7)

**Goal**: Sample ASP.NET Core app demonstrates realistic VectorTileHub integration

**Independent Test**: Run sample app, request tile for layer 82, verify valid MVT with curated whitelist attributes

### Implementation for User Story 7

- [X] T038 [P] [US7] Create `Program.cs` in `src/K1Soft.IT.VectorTileHub.Sample/`. Wire up VectorTileHub: `builder.Services.AddVectorTileHub(builder.Configuration)`, `builder.Services.AddVectorTileHubSqlServerProvider()`, `builder.Services.AddVectorTileHubJobs(builder.Configuration)`, `app.MapVectorTileHubEndpoints()`, `app.UseVectorTileHubHangfireDashboard()`. Add authentication middleware (simple JWT bearer or cookie auth for demo). See `specs/001-vector-tile-engine/quickstart.md` for the integration pattern

- [X] T039 [P] [US7] Create `appsettings.json` in `src/K1Soft.IT.VectorTileHub.Sample/`. Include the full `VectorTileHub` configuration section per `specs/001-vector-tile-engine/quickstart.md`: connection string `Default` pointing to `Server=127.0.0.1;Database=UALSDb;User ID=sa;Password=asdAAA123;TrustServerCertificate=True;Connection Timeout=3600`, route prefix `/vector-tile-hub`, layer config folder, cache root, SQLite settings store, Hangfire enabled with dashboard at `/vector-tile-hub/jobs`

- [X] T040 [US7] Create layer config file `src/K1Soft.IT.VectorTileHub.Sample/VectorTileHub/Layers/82-layer-data.json`. Configure layer 82 targeting `[UALSDb].[ualsdataview].[LayerData_82]` with geometry column `Geom`, SQL Server provider using connection string name `Default`, zoom range 12–21, extent 4096, buffer 64, clip geometry true. Attribute whitelist: `["Id", "LayerId", "DISTRICT", "SUBMUNICIPALITY", "SUBDISTRICT_NAME", "PARCELNUMBER", "PARCEL_LANDUSE", "LAND_USES", "PLAN_NUMBER", "BlockNumber"]`. This is a curated subset of the 50+ columns in the source table — see spec US7 acceptance scenario 2

**Checkpoint**: User Story 7 complete — sample application demonstrates full library integration.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Health checks, admin config reload, and final integration

- [X] T041 [P] Implement `VectorTileHubHealthCheck` in `src/K1Soft.IT.VectorTileHub.AspNetCore/HealthChecks/VectorTileHubHealthCheck.cs`. Implements `IHealthCheck`. Checks: (1) settings store is reachable (attempt a read from `IVectorTileRuntimeSettingsStore`), (2) cache root folder exists and is writable (attempt to create a temp file), (3) layer config folder exists and is readable. Returns `HealthCheckResult.Healthy` if all pass, `Unhealthy` with details if any fail. See `specs/001-vector-tile-engine/contracts/api-public.md` health check response format

- [X] T042 [P] Implement `AdminConfigEndpoints` in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/AdminConfigEndpoints.cs`. Maps `POST {RoutePrefix}/admin/layers/reload` per `specs/001-vector-tile-engine/contracts/api-admin.md`. Requires authorization. Calls `IVectorTileLayerConfigProvider.ReloadAsync()`, returns JSON with `layersLoaded`, `layersEnabled`, `layersDisabled`, `errors` array

- [X] T043 Verify solution builds and all projects compile. Run `dotnet build VectorTileHub.sln` from repository root. Fix any compilation errors, missing usings, or unresolved references

- [ ] T044 Run quickstart.md verification checklist against the sample application. Verify: app starts, layers endpoint returns metadata, tile endpoint returns MVT bytes, health check returns healthy, admin endpoints require auth, Hangfire dashboard is accessible. Document results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational — core MVP
- **US2 (Phase 4)**: Depends on US1 (extends orchestrator with scope)
- **US3 (Phase 5)**: Depends on US1 (extends orchestrator with cache)
- **US4 (Phase 6)**: Depends on US3 (cache must exist before cache management)
- **US5 (Phase 7)**: Depends on US1 (extends provider abstraction with Oracle)
- **US6 (Phase 8)**: Depends on Foundational only (metadata uses config provider)
- **US7 (Phase 9)**: Depends on US1 + US2 + US3 at minimum (sample needs core features)
- **Polish (Phase 10)**: Depends on all user stories

### User Story Dependencies

```text
Phase 1: Setup
    ↓
Phase 2: Foundational
    ↓
Phase 3: US1 (Tile Serving) ──────────────┐
    ↓           ↓           ↓              │
Phase 4: US2  Phase 5: US3  Phase 7: US5   Phase 8: US6
(Security)    (Cache)       (Oracle)       (Metadata)
                ↓
           Phase 6: US4
           (Cache Mgmt)
                ↓
           Phase 9: US7
           (Sample App)
                ↓
           Phase 10: Polish
```

### Parallel Opportunities

Within each phase, tasks marked [P] can run in parallel:

```text
Phase 2 parallel groups:
  Group A: T004, T005, T006, T010, T011 (independent files)
  Group B: T008 → T009 (sequential: entity before repository)
  T007 can run with Group A

Phase 3 parallel groups:
  Group A: T012, T013 (encoder + provider are independent)
  T014 after T013 (provider registration)
  T015 after T012 + T013 (orchestrator uses both)
  T016, T017, T018 after T015

Phase 5:
  T023, T024 in parallel (disk + memory cache are independent)
  T025 after both (composite wraps them)

Phase 6:
  T028, T029, T030 in parallel (independent job classes)
  T031 after T028 + T029 (swap uses generation + deletion)

Phase 9:
  T038, T039 in parallel (Program.cs + appsettings.json)
  T040 after both
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Serve a tile from SQL Server
5. Tiles work end-to-end with attribute whitelisting

### Incremental Delivery

1. Setup + Foundational → project compiles
2. US1 → tiles served from SQL Server (MVP)
3. US2 → tiles are scope-filtered
4. US3 → tiles are cached
5. US4 → cache managed via admin endpoints + background jobs
6. US5 → Oracle provider available
7. US6 → layer metadata discoverable
8. US7 → sample app demonstrates everything
9. Polish → health checks, config reload, verification

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each task includes exact file paths for Codex implementation
- Reference design documents in `specs/001-vector-tile-engine/` for detailed contracts
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently


