---
description: "Task list for VectorTileHub 002 — host-agnostic vector tile server library"
---

# Tasks: VectorTileHub — Host-Agnostic Vector Tile Server Library

**Input**: Design documents from `specs/002-vector-tile-server/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included (targeted contract/integration/unit tasks per story). The
project ships test projects and the acceptance scenarios in spec.md define
clear independent tests; write each story's tests first and ensure they fail
before implementing.

**Organization**: Tasks are grouped by user story (US1–US7) for independent
implementation and testing. The `src/` projects already exist (scaffolded for
001); this feature **evolves** them per plan.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1–US7); omitted for Setup/Foundational/Polish
- Exact file paths included

## Path Conventions

Repository root `D:\Code\VectorTileHub`. Library projects under `src/`, tests
under `tests/`. Namespace root `K1Soft.IT.VectorTileHub`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution + toolchain alignment to .NET 10 / `.slnx`

- [X] T001 Migrate the solution to `.slnx`: run `dotnet sln VectorTileHub.sln migrate`, verify all 8 `src/` projects + 3 `tests/` projects are listed in `VectorTileHub.slnx`, then remove `VectorTileHub.sln`
- [X] T002 Retarget every project to `net10.0` and `LangVersion` 14 via `Directory.Build.props` at repo root; confirm `dotnet build VectorTileHub.slnx` restores on SDK 10.0.300
- [ ] T003 [P] Centralize dependency versions in `Directory.Packages.props` (NetTopologySuite, NetTopologySuite.IO.VectorTiles, Microsoft.Data.SqlClient, Oracle.ManagedDataAccess.Core, Hangfire.Core, Hangfire.AspNetCore, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Caching.Memory, Swashbuckle.AspNetCore)
- [X] T004 [P] Add OpenLayers + `ol-mapbox-style` assets to the sample via CDN references in `src/K1Soft.IT.VectorTileHub.Sample/wwwroot/index.html` (placeholder shell created here, wired in US7)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Remove 001 security concepts, introduce the variant model and shared abstractions every story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Remove 001 security components: delete `IVectorTileSecurityScopeResolver.cs`, `Models/VectorTileSecurityScope.cs`, `Services/DefaultSecurityScopeResolver.cs`, and the `SecurityConfig` type, plus all references in DI/orchestrator (`src/K1Soft.IT.VectorTileHub.Abstractions/`, `src/K1Soft.IT.VectorTileHub.Core/`)
- [X] T006 [P] Add `Models/CacheRuleConfig.cs` and `Models/FilterConfig.cs` (variant key, IsDefault, parameterized Column/Operator/Values) in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`
- [X] T007 [P] Add `Models/VectorTileVariant.cs` (VariantKey, ResolvedFilter?, IsDefault) in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`
- [X] T008 [P] Update `Models/VectorTileLayerConfig.cs` to carry `CacheRules: CacheRuleConfig[]` (remove `Security`) in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`
- [X] T009 [P] Update `Models/VectorTileHubOptions.cs` (add `LayerConfigPaths`, remove `DefaultAuthenticationRequired`), `SettingsStoreOptions` (nullable ConnectionString), `HangfireOptions` (remove `RequiredRoles`) in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`
- [X] T010 [P] Update `Models/VectorTileCacheKey.cs` (VariantKey replaces ScopeKey + path/string repr), `Models/VectorTileResult.cs` (add `IsStale`), `Models/VectorTileFeatureQuery.cs` (Variant replaces SecurityScope) in `src/K1Soft.IT.VectorTileHub.Abstractions/Models/`
- [X] T011 Define/Update interfaces in `src/K1Soft.IT.VectorTileHub.Abstractions/Interfaces/`: add `IVectorTileVariantResolver.cs`; update `IVectorTileService` (add `variantKey`), `IVectorTileCache` (CachedTile.WrittenAt, RemoveVariantAsync), `IVectorTileRuntimeSettingsStore` (global key/value + per layer+variant), `IVectorTileLayerConfigProvider` (Reload), and add job interfaces (`ICacheGenerationJob`, `ICacheInvalidationJob`, `ICacheSwapBuildJob`, `ICacheDeletionJob`) per `contracts/interfaces.md`
- [X] T012 Implement `TileMath/TileCoordinateUtils.cs` (XYZ↔EPSG:3857, buffer expansion, bbox→tile-coverage across a zoom range) in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T013 Implement `Services/JsonLayerConfigProvider.cs` to load layer files from `LayerConfigPaths` + `LayerConfigFolder` (basic load + GetLayer/GetLayers) in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T014 DI + routing skeleton: `VectorTileHubCoreServiceCollectionExtensions.cs`, `VectorTileHubServiceCollectionExtensions.cs` (AddVectorTileHub), `VectorTileHubEndpointRouteBuilderExtensions.cs` (MapVectorTileHubEndpoints), plus RFC7807 problem-details + structured logging + `VectorTileHubHealthCheck` scaffolding across `src/K1Soft.IT.VectorTileHub.Core/DependencyInjection/` and `src/K1Soft.IT.VectorTileHub.AspNetCore/`

**Checkpoint**: Abstractions compile; layers can be loaded and resolved; DI/routing skeleton in place.

---

## Phase 3: User Story 1 — Serve Vector Tiles from a Configured Layer (Priority: P1) 🎯 MVP

**Goal**: Return a valid MVT/PBF tile for (layerId, z, x, y), empty tile outside zoom/empty areas, 404 for unknown layer.

**Independent Test**: Configure one SQL Server layer, request a tile at a coordinate with data → valid PBF; request outside zoom range → empty tile.

### Tests for User Story 1

- [ ] T015 [P] [US1] Integration test: GET `tiles/{id}/{z}/{x}/{y}.pbf` returns valid PBF for a layer with data, in `tests/K1Soft.IT.VectorTileHub.AspNetCore.Tests/TileEndpointTests.cs`
- [ ] T016 [P] [US1] Unit test: empty/blank tile outside `[MinZoom,MaxZoom]` and for empty areas, plus unknown-layer 404, in `tests/K1Soft.IT.VectorTileHub.Core.Tests/OrchestratorServeTests.cs`

### Implementation for User Story 1

- [X] T017 [P] [US1] Implement `IVectorTileEncoder` + `Encoding/MapboxVectorTileEncoder.cs` (`Encode` + `EmptyTile`, extent/buffer/clip) in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T018 [P] [US1] Implement `SqlServerFeatureProvider.cs` parameterized bbox spatial query → WKB + whitelisted attributes in `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/`
- [X] T019 [US1] Implement disk cache read path in `Services/DiskTileCache.cs` (`GetAsync` returning `CachedTile` with `WrittenAt`) in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T020 [US1] Implement `Services/DefaultVariantResolver.cs` returning the default variant when no key is supplied, in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T021 [US1] Implement `Services/VectorTileOrchestrator.cs` serving path: resolve layer → variant → cache lookup → on-demand encode on miss → empty tile outside zoom, in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T022 [US1] Implement `Endpoints/TileEndpoints.cs` GET `tiles/{layerId}/{z}/{x}/{y}.pbf` (optional `variant` query) and wire into the route builder, in `src/K1Soft.IT.VectorTileHub.AspNetCore/`
- [X] T023 [US1] Implement `Endpoints/LayerMetadataEndpoints.cs` GET `layers` and `layers/{layerId}` (no connection strings) in `src/K1Soft.IT.VectorTileHub.AspNetCore/`
- [X] T024 [US1] Add response headers (`X-VTH-From-Cache`/`X-VTH-Stale`), content type `application/x-protobuf`, and 404 problem result for unknown layer in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/TileEndpoints.cs`

**Checkpoint**: A single configured SQL Server layer serves valid tiles; MVP demoable.

---

## Phase 4: User Story 2 — External Layer Config with Independent Connections (Priority: P1)

**Goal**: Load layers from files anywhere, each with its own connection string, supporting SQL Server and Oracle; validate; reject duplicate ids.

**Independent Test**: Two files in different folders (one SQL Server, one Oracle) load and are addressable by id; a missing field and a duplicate id are reported with file/field detail.

### Tests for User Story 2

- [ ] T025 [P] [US2] Integration test: two layer files (SQL Server + Oracle) in separate directories both load and resolve by id, in `tests/K1Soft.IT.VectorTileHub.Integration.Tests/MultiProviderConfigTests.cs`
- [ ] T026 [P] [US2] Unit test: missing required field and duplicate integer id are rejected with file- and field-specific errors while other layers still load, in `tests/K1Soft.IT.VectorTileHub.Core.Tests/LayerConfigValidationTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] Extend `Services/JsonLayerConfigProvider.cs`: load from arbitrary absolute paths, validate required fields, reject duplicate `Id`, collect per-file errors without aborting other layers, in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T028 [P] [US2] Per-layer connection resolution (named `ConnectionStringName` or direct `ConnectionString`) in `src/K1Soft.IT.VectorTileHub.Core/Services/` (helper used by providers)
- [X] T029 [P] [US2] Implement `OracleFeatureProvider.cs` parameterized `SDO_RELATE` spatial query → WKB + whitelisted attributes in `src/K1Soft.IT.VectorTileHub.Providers.Oracle/`
- [X] T030 [US2] Provider registry mapping `ProviderConfig.Type` → provider and wire `AddVectorTileHubSqlServerProvider`/`AddVectorTileHubOracleProvider` DI extensions in `src/K1Soft.IT.VectorTileHub.Providers.*/DependencyInjection/`
- [X] T031 [US2] Expose available variant keys + zoom range + attribute whitelist in `Endpoints/LayerMetadataEndpoints.cs` (no secrets) in `src/K1Soft.IT.VectorTileHub.AspNetCore/`

**Checkpoint**: Multiple layers across both providers load from external files and serve.

---

## Phase 5: User Story 3 — Cache Lifecycle (Priority: P2)

**Goal**: Generate, on-demand fill, notify-by-bbox refresh, blue/green replace, delete — all as per-layer background jobs; stale-while-revalidate.

**Independent Test**: Generate populates the cache folder; uncached request is generated/persisted; bbox notify refreshes intersecting tiles; swap never interrupts serving; stale tile served immediately then refreshed.

### Tests for User Story 3

- [ ] T032 [P] [US3] Integration tests: cache generation populates folder; on-demand miss persists; blue/green swap serves old-or-new (never partial); stale-while-revalidate serves stale + enqueues refresh, in `tests/K1Soft.IT.VectorTileHub.Integration.Tests/CacheLifecycleTests.cs`

### Implementation for User Story 3

- [X] T033 [US3] Complete `Services/DiskTileCache.cs` write/remove/`RemoveVariantAsync` with `{LayerId}/{VariantKey}/{CacheVersion}/{z}/{x}/{y}.pbf` layout in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T034 [P] [US3] Implement `Services/MemoryTileCache.cs` and `Services/CompositeTileCache.cs` (memory + disk) in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T035 [US3] Add on-demand generation + persist with single-flight de-duplication (no duplicate writes for concurrent misses) to `Services/VectorTileOrchestrator.cs` in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T036 [US3] Implement stale-while-revalidate in `Services/VectorTileOrchestrator.cs`: serve stale, enqueue one refresh per tile (pending-refresh guard), set `VectorTileResult.IsStale`, in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T037 [P] [US3] Implement `CacheGenerationJob.cs` (layer/variant/zoom-range/version) in `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T038 [P] [US3] Implement `CacheInvalidationJob.cs` (bbox → tile coverage refresh) in `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T039 [US3] Implement `CacheSwapJob.cs` job A (build into new empty version folder + flip active version) in `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T040 [US3] Implement `CacheDeletionJob.cs` job B (delete previous version folder / delete cache) in `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T041 [US3] Implement `Endpoints/AdminCacheEndpoints.cs` generate/swap/delete/notify/status returning 202 + job ids (no built-in auth) in `src/K1Soft.IT.VectorTileHub.AspNetCore/`
- [X] T042 [US3] Job failure handling: mark `Failed`, retain already-written tiles, support retry, in `src/K1Soft.IT.VectorTileHub.Jobs/` + orchestrator status updates

**Checkpoint**: Full cache lifecycle works with background jobs; no serving interruption on swap.

---

## Phase 6: User Story 4 — Persist & Fast-Serve Server Settings (Priority: P2)

**Goal**: Durable settings in a DB table (host connection or auto SQLite), mirrored in memory with write-through.

**Independent Test**: No connection → SQLite auto-created/seeded; changing the active cache folder persists and updates memory; repeated reads avoid the DB.

### Tests for User Story 4

- [ ] T043 [P] [US4] Integration test: SQLite auto-create when no connection; setting change persisted to table + reflected in memory; unchanged reads do not hit the DB, in `tests/K1Soft.IT.VectorTileHub.Integration.Tests/SettingsStoreTests.cs`

### Implementation for User Story 4

- [X] T044 [US4] Implement `VectorTileHubDbContext.cs` + `Entities/LayerVariantRuntimeSettingsEntity.cs` + `Entities/ServerSettingEntity.cs` + initial migration in `src/K1Soft.IT.VectorTileHub.Storage/`
- [X] T045 [US4] Implement `Repositories/EfRuntimeSettingsStore.cs` with SQLite auto-create (or host-supplied connection) and CRUD in `src/K1Soft.IT.VectorTileHub.Storage/`
- [X] T046 [US4] Add in-memory mirror with write-through (`GetSetting`/`SetSettingAsync` cache-on-read, refresh-on-write) in `src/K1Soft.IT.VectorTileHub.Storage/Repositories/EfRuntimeSettingsStore.cs`
- [ ] T047 [US4] Wire `ActiveCacheRootPath` + active cache version reads/writes through the orchestrator and cache jobs in `src/K1Soft.IT.VectorTileHub.Core/` and `src/K1Soft.IT.VectorTileHub.Jobs/`

**Checkpoint**: Settings persist durably and serve from memory; cache swap uses persisted active version.

---

## Phase 7: User Story 5 — Managed Background Jobs + Dashboard (Priority: P3)

**Goal**: Cache work runs on Hangfire with a dashboard; jobs attributable per layer+variant; dashboard authorization is host-supplied.

**Independent Test**: Enqueue several per-layer jobs and see them in the dashboard tagged by layer+variant with state; a host-supplied auth filter governs dashboard access.

### Tests for User Story 5

- [ ] T048 [P] [US5] Integration test: jobs enqueue and surface per layer+variant with state; a host-supplied dashboard authorization filter decides access, in `tests/K1Soft.IT.VectorTileHub.AspNetCore.Tests/JobDashboardTests.cs`

### Implementation for User Story 5

- [X] T049 [US5] Implement `DependencyInjection/JobsServiceCollectionExtensions.cs` (Hangfire storage config + job DI registration) in `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T050 [US5] Tag jobs per layer+variant and surface state in `Endpoints/AdminCacheEndpoints.cs` `cache/status` in `src/K1Soft.IT.VectorTileHub.AspNetCore/`
- [X] T051 [US5] Implement `UseVectorTileHubHangfireDashboard(...)` accepting a host-supplied `IDashboardAuthorizationFilter`/delegate, enforcing no built-in policy, in `src/K1Soft.IT.VectorTileHub.AspNetCore/DependencyInjection/VectorTileHubEndpointRouteBuilderExtensions.cs`

**Checkpoint**: Background jobs observable; host controls dashboard access.

---

## Phase 8: User Story 6 — Filter-Scoped Cache Variants (Priority: P3)

**Goal**: Layers produce independent filtered cache variants selected by a variant key; library stays role-agnostic.

**Independent Test**: A layer with two filter rules produces two independent caches; a variant key returns only matching features; unknown key → "variant not found"; no key → default.

### Tests for User Story 6

- [ ] T052 [P] [US6] Integration test: two filter variants build independent caches; `?variant=` returns filtered features; unknown key → 404 "variant not found"; omitted key → default, in `tests/K1Soft.IT.VectorTileHub.Integration.Tests/VariantCacheTests.cs`

### Implementation for User Story 6

- [X] T053 [P] [US6] Apply the variant filter as a parameterized predicate in `SqlServerFeatureProvider.cs` in `src/K1Soft.IT.VectorTileHub.Providers.SqlServer/`
- [X] T054 [P] [US6] Apply the variant filter as parameterized bind variables in `OracleFeatureProvider.cs` in `src/K1Soft.IT.VectorTileHub.Providers.Oracle/`
- [X] T055 [US6] Extend `Services/DefaultVariantResolver.cs`: resolve key → cache rule, unknown key → null ("variant not found"), enforce single default, in `src/K1Soft.IT.VectorTileHub.Core/`
- [X] T056 [US6] Ensure variant-scoped cache keys/paths and per-variant generation/status across `Services/DiskTileCache.cs`, `Services/VectorTileOrchestrator.cs`, and the cache jobs in `src/K1Soft.IT.VectorTileHub.Core/` + `src/K1Soft.IT.VectorTileHub.Jobs/`
- [X] T057 [US6] Thread `variant` end-to-end through tile + admin endpoints, returning 404 problem on unknown variant, in `src/K1Soft.IT.VectorTileHub.AspNetCore/Endpoints/`

**Checkpoint**: Variant-scoped serving and caching work end-to-end.

---

## Phase 9: User Story 7 — Documented API + OpenLayers Sample (Priority: P3)

**Goal**: Every endpoint documented; sample exposes Swagger and renders the served PBF tiles in OpenLayers using a style generated from `tmp/layerStyle.sld`.

**Independent Test**: Swagger lists/exercises every endpoint; the OpenLayers page renders tiles with SLD-equivalent symbology (39 rules + scale-limited label).

### Tests for User Story 7

- [ ] T058 [P] [US7] Test: `SldToStyleConverter` reproduces all 39 SLD rules (fill+line, `ogc:Or`→`in`) and the parcel label scale, in `tests/K1Soft.IT.VectorTileHub.Core.Tests/SldToStyleConverterTests.cs`

### Implementation for User Story 7

- [X] T059 [US7] Add Swashbuckle to the sample and document every endpoint (params, responses, examples) in `src/K1Soft.IT.VectorTileHub.Sample/Program.cs` (+ XML doc comments on public AspNetCore endpoints)
- [X] T060 [P] [US7] Implement `Tools/SldToStyleConverter.cs`: parse `tmp/layerStyle.sld` → Mapbox GL JSON per `contracts/sld-style.md` (one fill + line layer per rule, `ogc:Or`→`in`, label `symbol` layer with `minzoom` from `MaxScaleDenominator`) in `src/K1Soft.IT.VectorTileHub.Sample/`
- [X] T061 [US7] Add a sample CLI command `gen-style <sld> <out>` that writes `wwwroot/ol-style.json`, in `src/K1Soft.IT.VectorTileHub.Sample/Program.cs`
- [X] T062 [US7] Implement `wwwroot/index.html` + `wwwroot/app.js` using OpenLayers + `ol-mapbox-style` to load `ol-style.json` and render tiles from `/vector-tile-hub/tiles/...`, in `src/K1Soft.IT.VectorTileHub.Sample/wwwroot/`
- [X] T063 [P] [US7] Add sample `VectorTileHub/Layers/82-layer-data.json` (with cache variants) and `appsettings.json` per `quickstart.md`, in `src/K1Soft.IT.VectorTileHub.Sample/`

**Checkpoint**: Sample documents all endpoints and renders styled tiles in the browser.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Packaging, performance, docs, and final compliance checks across all stories.

- [X] T064 [P] Unit tests for `TileCoordinateUtils` (XYZ↔3857 round-trip, buffer, bbox coverage edges) in `tests/K1Soft.IT.VectorTileHub.Core.Tests/TileCoordinateUtilsTests.cs`
- [ ] T065 [P] Add NuGet packaging metadata (PackageId, Description, README, version) to all `src/K1Soft.IT.VectorTileHub.*` library `.csproj` files (exclude the Sample)
- [ ] T066 Performance pass: confirm the cached request path performs no EF Core/source-DB access and avoids unnecessary allocations, in `src/K1Soft.IT.VectorTileHub.Core/Services/VectorTileOrchestrator.cs`
- [X] T067 Security/constitution check: verify attribute whitelist enforcement, parameterized SQL only, no client SQL fragments, host-owned auth, across providers and endpoints
- [ ] T068 [P] Update `specs/002-vector-tile-server/quickstart.md` references and add a top-level `README.md` usage section
- [ ] T069 Run `quickstart.md` end-to-end against the sample (build `.slnx`, serve a tile, generate style, render in OpenLayers) and record results
- [X] T070 Remove obsolete 001 artifacts/dead config (security settings, scope references) remaining anywhere under `src/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Stories (Phases 3–9)**: All depend on Foundational.
  - US1 (P1) → MVP. US2 (P1) builds out external config + Oracle.
  - US3, US4 (P2) depend on Foundational; US3 uses orchestrator/cache from US1; US4 is largely independent (settings).
  - US5, US6, US7 (P3): US5 needs US3's jobs; US6 needs US1/US2 (providers) + US3 (cache); US7 needs US1 (tiles) for the render demo.
- **Polish (Phase 10)**: After desired stories complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational. Core serving path. No story deps.
- **US2 (P1)**: After Foundational. Enriches config + adds Oracle. Independent of US1 internals but shares the config provider.
- **US3 (P2)**: After Foundational; integrates US1 orchestrator/cache.
- **US4 (P2)**: After Foundational; independent; US3 consumes its active-version state.
- **US5 (P3)**: After US3 (jobs exist to manage/observe).
- **US6 (P3)**: After US1/US2 (providers) and US3 (cache) — variant filtering + variant-scoped caches.
- **US7 (P3)**: After US1 (tiles to render) — docs + OpenLayers + SLD style.

### Within Each User Story

- Tests first (write failing) → models → services → endpoints → integration.
- Models before services; services before endpoints.

### Parallel Opportunities

- Setup: T003, T004 in parallel.
- Foundational: T006–T010 (distinct model files) in parallel after T005; T011–T013 follow.
- US1: T015/T016 (tests) parallel; T017/T018 parallel (encoder vs provider).
- US2: T025/T026 parallel; T028/T029 parallel.
- US3: T037/T038 parallel; T034 parallel with job work.
- US6: T053/T054 parallel (two providers).
- Across teams: once Foundational completes, US1+US2 (P1) and US4 (independent) can proceed in parallel; US3 then unlocks US5/US6.

---

## Parallel Example: User Story 1

```text
# Tests (write first, expect fail):
Task: "Integration test GET tiles returns PBF in tests/K1Soft.IT.VectorTileHub.AspNetCore.Tests/TileEndpointTests.cs"
Task: "Unit test empty tile/zoom/404 in tests/K1Soft.IT.VectorTileHub.Core.Tests/OrchestratorServeTests.cs"

# Then parallel implementation:
Task: "MapboxVectorTileEncoder in src/K1Soft.IT.VectorTileHub.Core/Encoding/MapboxVectorTileEncoder.cs"
Task: "SqlServerFeatureProvider in src/K1Soft.IT.VectorTileHub.Providers.SqlServer/SqlServerFeatureProvider.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 — both P1)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 US1 (serve one SQL Server layer) → STOP & VALIDATE (valid PBF, empty tile, 404).
3. Phase 4 US2 (external files + Oracle + validation) → VALIDATE multi-provider.
4. Demo the P1 MVP.

### Incremental Delivery

- Add US3 (cache lifecycle) → demo background generation + blue/green.
- Add US4 (settings) → demo persisted active version + memory mirror.
- Add US5 (jobs/dashboard), US6 (variants), US7 (docs + OpenLayers) → demo each.

### Notes

- `[P]` = different files, no incomplete dependencies.
- The `src/` projects already exist (001) — most tasks **modify** existing files per plan.md deltas rather than create from scratch.
- Constitution deviation (host-owned security) is intentional and documented in plan.md; keep all server-side data-leak protections (whitelist, parameterized SQL) intact.
- Commit after each task or logical group; stop at any checkpoint to validate independently.
