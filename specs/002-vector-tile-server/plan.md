# Implementation Plan: VectorTileHub — Host-Agnostic Vector Tile Server Library

**Branch**: `002-vector-tile-server` | **Date**: 2026-06-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-vector-tile-server/spec.md`

## Summary

Build (evolve) a reusable ASP.NET Core library (`K1Soft.IT.VectorTileHub`),
shipped as NuGet packages, that produces **MVT/PBF vector tiles** from
provider-agnostic data sources (SQL Server + Oracle). Compared to feature
001, this revision makes the library **host-agnostic for security**: the
library performs **no authentication, no authorization, and no
role/scope resolution of its own**. Instead, a tile request carries an
optional **variant key** that selects one of a layer's configured
**filtered cache variants**; the host application maps user roles to
variant keys before calling. Caching is disk-based with safe blue/green
replacement, on-demand fill, bounding-box invalidation, and
**stale-while-revalidate** refresh, all orchestrated through Hangfire
background jobs whose dashboard authorization is supplied entirely by the
host. Runtime settings persist in an internal store (SQLite by default,
or a host-supplied connection) mirrored in memory. A **sample ASP.NET
Core app** demonstrates every endpoint via Swagger and renders the served
PBF tiles in an **OpenLayers** page using a style **generated from the
supplied `tmp/layerStyle.sld`**.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0 (SDK 10.0.300 present)

**Primary Dependencies**:
- NetTopologySuite 2.x — geometry model and spatial operations
- NetTopologySuite.IO.VectorTiles (Mapbox) — MVT/PBF encoding
- Microsoft.Data.SqlClient — SQL Server ADO.NET provider (hot path)
- Oracle.ManagedDataAccess.Core — Oracle ADO.NET provider (hot path)
- Hangfire.Core + Hangfire.AspNetCore — background jobs + dashboard
- Microsoft.EntityFrameworkCore.Sqlite — internal runtime settings store
- Microsoft.Extensions.Caching.Memory — optional memory cache + settings mirror
- Microsoft.Extensions.Diagnostics.HealthChecks — health indicator
- Swashbuckle.AspNetCore — Swagger for the sample project
- (sample, front-end) OpenLayers + ol-mapbox-style — render MVT with a style

**Storage**:
- SQL Server / Oracle (tile feature sources, per layer; hot path = ADO.NET)
- SQLite (default internal runtime settings store; or host-supplied connection)
- Disk filesystem (tile cache, blue/green version folders)
- In-memory (optional tile cache layer + settings mirror)

**Testing**: xUnit + Microsoft.AspNetCore.Mvc.Testing + NSubstitute; spatial
provider integration tests gated behind opt-in connection strings.

**Target Platform**: Windows / Linux server (ASP.NET Core cross-platform)

**Project Type**: Reusable NuGet library suite + sample web application

**Performance Goals**: Cached-tile responses served from disk/memory without
touching the source database on the request path; sustain high concurrent
cached-tile request volume without degradation. Concrete latency/throughput
targets deferred to load testing (spec Deferred items).

**Constraints**: No security/auth/role logic in the library (host owns it);
parameterized SQL only; no EF Core on the hot-path tile query; attribute
whitelisting mandatory; stale-while-revalidate must keep the request path
fast; blue/green cache swap must never serve a partial tile.

**Scale/Scope**: Multi-layer, multi-provider, variant-aware caching,
single-server deployment for this version.

**Solution format**: `.slnx` (XML solution format), per explicit requirement;
replaces the existing `VectorTileHub.sln`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The constitution (v1.0.0) was written for feature 001, which placed security
scope resolution **inside** the library. Feature 002, per the spec owner's
explicit and clarified direction, moves the **security policy decision** to
the host. This affects Principle IV and the Configuration standard. The
security-critical **mechanisms** remain in the library and remain
server-side; only the **policy decision (who → which variant)** moves to the
host (itself a trusted server-side component). The deviation is recorded in
**Complexity Tracking** below.

| # | Principle / Standard | Status | Notes |
|---|----------------------|--------|-------|
| I | Library-First Rule | PASS | All core capabilities live in library projects; sample is only a consumer + render demo. |
| II | Separation of Concerns | PASS | Endpoint handling / orchestration / cache / settings / config loading / feature retrieval / encoding / jobs stay in distinct components. |
| III | Policy-Driven Request Handling | PASS (adapted) | The 7-step pipeline is preserved, but step "which security scope applies" becomes "which **variant key** was supplied" — resolved by the host, applied by the library. |
| IV | Security (NON-NEGOTIABLE) | PASS w/ documented deviation | Data-leak protections kept: whitelist-only attributes, parameterized SQL, server-side variant filtering, never trusting the browser. **Deviation**: auth + role→variant resolution moved out of the library to the host (a trusted server). See Complexity Tracking. |
| V | Provider Independence | PASS | Encoder/cache know nothing of SQL Server/Oracle/HTTP; providers don't own cache or endpoint behavior. |
| VI | Performance | PASS | ADO.NET on hot path; EF Core only for settings; stale-while-revalidate keeps request path off the DB. |
| Cfg | Configuration | PASS (adapted) | Route prefix, layers, cache roots, providers, settings store, job behavior all external. **Security policy mappings removed** from library config (now host-owned). Dashboard authorization is host-supplied. |
| Tile | Tile Output | PASS | XYZ addressing, EPSG:3857 default, extent 4096, buffer 64, whitelist-only. |
| Cache | Cache | PASS | Cache key = layer + z/x/y + **variant** + version; disk + optional memory; blue/green immediate cutover with background rebuild/delete. |
| SQL | SQL | PASS | Parameterized only; trusted server-side filter construction; no client SQL fragments. |
| RT | Runtime Settings | PASS | Durable SQLite/host store is source of truth; memory mirror for fast reads. |
| Sample | Sample Application | PASS | Separate project; demonstrates provider registration, layer config, tile endpoints, SQL Server integration, Swagger, **and OpenLayers rendering**. |
| FV | First-Version Discipline | PASS | SQL Server + Oracle; no distributed complexity; no overbuilt admin UI. |
| DO | Decision Order | PASS | Security mechanisms preserved first; the policy-location change is a deliberate, documented reusability decision by the spec owner. |

**Gate result**: PASS — one deliberate, justified deviation from Principle IV
(policy-decision location), documented in Complexity Tracking. No data-leak
protections are weakened.

## Project Structure

### Documentation (this feature)

```text
specs/002-vector-tile-server/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── api-public.md    # Tile + layer-metadata endpoints
│   ├── api-admin.md     # Cache lifecycle / notify / config-reload endpoints
│   ├── interfaces.md    # Library extension-point interfaces
│   └── sld-style.md     # SLD → OpenLayers style mapping (sample)
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

The 001 projects already exist under `src/`. This plan **evolves** them:
removes the security-scope components, introduces variant/cache-rule
concepts, switches the solution to `.slnx`, and extends the sample with an
OpenLayers render page + an SLD→style generator.

```text
VectorTileHub.slnx                      # NEW: .slnx solution (replaces .sln)

src/
├── K1Soft.IT.VectorTileHub.Abstractions/
│   ├── Interfaces/
│   │   ├── IVectorTileService.cs
│   │   ├── IVectorTileFeatureProvider.cs
│   │   ├── IVectorTileEncoder.cs
│   │   ├── IVectorTileCache.cs
│   │   ├── IVectorTileRuntimeSettingsStore.cs
│   │   ├── IVectorTileLayerConfigProvider.cs
│   │   └── IVectorTileVariantResolver.cs        # NEW: maps variant key → cache rule (no auth)
│   │   #   REMOVED: IVectorTileSecurityScopeResolver.cs
│   └── Models/
│       ├── VectorTileFeature.cs
│       ├── VectorTileFeatureBatch.cs
│       ├── VectorTileFeatureQuery.cs
│       ├── VectorTileResult.cs
│       ├── VectorTileCacheKey.cs                # variant key replaces scope key
│       ├── VectorTileCacheOptions.cs
│       ├── VectorTileEncodingContext.cs
│       ├── VectorTileLayerConfig.cs             # CacheRules[] replace Security
│       ├── CacheRuleConfig.cs                   # NEW
│       ├── VectorTileVariant.cs                 # NEW (resolved variant)
│       ├── VectorTileLayerRuntimeSettings.cs
│       └── VectorTileHubOptions.cs              # security defaults removed
│
├── K1Soft.IT.VectorTileHub.Core/
│   ├── Services/
│   │   ├── VectorTileOrchestrator.cs            # adds stale-while-revalidate
│   │   ├── DiskTileCache.cs
│   │   ├── MemoryTileCache.cs
│   │   ├── CompositeTileCache.cs
│   │   ├── DefaultVariantResolver.cs            # NEW (replaces scope resolver)
│   │   └── JsonLayerConfigProvider.cs
│   ├── Encoding/
│   │   └── MapboxVectorTileEncoder.cs
│   ├── TileMath/
│   │   └── TileCoordinateUtils.cs               # bbox → tile coverage for notify
│   └── DependencyInjection/
│       └── VectorTileHubCoreServiceCollectionExtensions.cs
│
├── K1Soft.IT.VectorTileHub.AspNetCore/
│   ├── Endpoints/
│   │   ├── TileEndpoints.cs                     # variantKey param added
│   │   ├── LayerMetadataEndpoints.cs
│   │   ├── AdminCacheEndpoints.cs               # generate/delete/swap/notify (no built-in auth)
│   │   └── AdminConfigEndpoints.cs              # explicit reload
│   ├── HealthChecks/
│   │   └── VectorTileHubHealthCheck.cs
│   └── DependencyInjection/
│       ├── VectorTileHubServiceCollectionExtensions.cs
│       └── VectorTileHubEndpointRouteBuilderExtensions.cs   # host supplies dashboard auth filter
│
├── K1Soft.IT.VectorTileHub.Providers.SqlServer/
│   └── SqlServerFeatureProvider.cs              # applies variant filter (parameterized)
│
├── K1Soft.IT.VectorTileHub.Providers.Oracle/
│   └── OracleFeatureProvider.cs                 # applies variant filter (parameterized)
│
├── K1Soft.IT.VectorTileHub.Storage/
│   ├── VectorTileHubDbContext.cs
│   ├── Entities/LayerRuntimeSettingsEntity.cs
│   └── Repositories/EfRuntimeSettingsStore.cs   # write-through + memory mirror
│
├── K1Soft.IT.VectorTileHub.Jobs/
│   ├── CacheGenerationJob.cs
│   ├── CacheDeletionJob.cs
│   ├── CacheInvalidationJob.cs                  # bbox → tiles refresh + stale refresh
│   └── CacheSwapJob.cs                          # build-new-folder + delete-old (two jobs)
│
└── K1Soft.IT.VectorTileHub.Sample/
    ├── Program.cs                               # Swagger + endpoints + dashboard auth
    ├── appsettings.json
    ├── VectorTileHub/Layers/82-layer-data.json  # incl. cache variants
    ├── Tools/SldToStyleConverter.cs             # NEW: SLD → OpenLayers/Mapbox-GL style
    └── wwwroot/
        ├── index.html                           # NEW: OpenLayers map page
        ├── ol-style.json                        # NEW: generated style (from layerStyle.sld)
        └── app.js                               # NEW: OL + ol-mapbox-style wiring

tests/
├── K1Soft.IT.VectorTileHub.Core.Tests/          # orchestrator, cache, variant resolver, tile math
├── K1Soft.IT.VectorTileHub.AspNetCore.Tests/    # endpoints (incl. variantKey, empty tile, not found)
└── K1Soft.IT.VectorTileHub.Integration.Tests/   # provider + blue/green + stale-revalidate
```

**Structure Decision**: Keep the constitution-aligned multi-project layout
already established in `src/`. The deltas are surgical: remove the
security-scope abstraction, add a provider-agnostic **variant** concept
(key + parameterized filter) used in cache keys and provider queries, make
dashboard authorization a host-supplied delegate, add stale-while-revalidate
to the orchestrator, convert the solution to `.slnx`, and extend the sample
with an OpenLayers render page fed by an SLD-derived style.

## Complexity Tracking

> Filled because the Constitution Check records one deliberate deviation.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Principle IV deviation: authentication + role→variant **policy decision** moved out of the library to the host (vs. 001's in-library `SecurityScopeResolver`) | The spec owner explicitly redirected (002) so the library is host-agnostic — the host already owns identity/roles and exposes/secures endpoints; duplicating policy in the library would force an identity model on every host. Security **mechanisms** (whitelist, parameterized SQL, server-side variant filtering, no browser trust) stay in the library. | Keeping in-library scope resolution (001) would re-impose a security/identity coupling the spec explicitly removes, reducing reusability — the top non-security item in the Decision Order. The non-negotiable property ("unauthorized records never encoded") is still enforced via the host-selected variant's server-side filter. |
| Library config no longer carries security policy mappings / `DefaultAuthenticationRequired` | Same as above — those are host concerns now. | Retaining them would be dead/ignored config inviting the false belief the library enforces auth. |
