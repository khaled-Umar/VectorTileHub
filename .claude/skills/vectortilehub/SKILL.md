---
name: "vectortilehub"
description: "Integrate and use the VectorTileHub library (K1Soft.IT.VectorTileHub) to serve cache-aware Mapbox Vector Tiles (MVT/PBF) from SQL Server, Oracle, or PostGIS in an ASP.NET Core host. Use when adding vector-tile serving, wiring AddVectorTileHubServer / a DB provider, writing tile/layer/cache-admin controllers, defining layer JSON config, configuring caching/Hangfire pre-generation, variants (filtered tiles), or troubleshooting slow/204/empty tiles."
compatibility: "ASP.NET Core host on .NET 10. Requires a spatial DB (SQL Server 2016+, Oracle Spatial, or PostgreSQL/PostGIS) with geometries in EPSG:4326 or 3857."
metadata:
  author: "K1Soft.IT"
  source: "docs/developer-tutorial.md"
user-invocable: true
disable-model-invocation: false
---

# Using the VectorTileHub library

`K1Soft.IT.VectorTileHub` is a **service library, not a framework**. It serves cache-aware
Mapbox Vector Tiles (MVT/PBF) from a spatial database. Follow this skill when integrating it
into an ASP.NET Core host or operating its caches.

## The one rule that drives everything

**The library exposes NO HTTP endpoints.** It registers injectable services; the **host**
owns the HTTP surface — you write the controllers, choose the routes, and apply all
auth. The library performs **no** authentication/authorization. Per this repo's convention,
endpoints are **MVC controllers** (`[ApiController]` + `ControllerBase` + `[HttpGet]`/`[HttpPost]`,
mapped via `app.MapControllers()`) — **never** minimal APIs (`app.MapGet`/`MapPost`).

Three services are all you consume:

| Service | Purpose |
|---|---|
| `IVectorTileService` | `GetTileAsync(layerId, z, x, y, variant?, ct)` → an MVT tile. |
| `IVectorTileLayerConfigProvider` | Read layer metadata (`GetLayer`, `GetAllLayers`, `ReloadAsync`). |
| `IVectorTileCacheAdmin` | Cache ops: generate / delete / invalidate / notify-change / swap / status. |

Root namespace: `K1Soft.IT.VectorTileHub`. Targets **.NET 10**.

## Integration checklist (do these in order)

1. **Reference two NuGet packages** from the local feed `D:\OneDrive\NUGetLocalPackages`:
   the facade `K1Soft.IT.VectorTileHub.Server` (brings Core/Storage/Jobs/AspNetCore/Abstractions
   transitively) **plus exactly one provider** (`.Providers.SqlServer`, `.Providers.Oracle`, or
   `.Providers.Postgis`). Mirrors `EFCore` + `EFCore.SqlServer`. Host project must use
   `<Project Sdk="Microsoft.NET.Sdk.Web">`.

   **a. Register the local feed** — add a `nuget.config` next to your `.sln` (or solution folder):

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="LocalPackages" value="D:\OneDrive\NUGetLocalPackages" />
     </packageSources>
   </configuration>
   ```

   **b. Add the package references** to the host `.csproj` (current published version is `1.0.0`):

   ```xml
   <ItemGroup>
     <PackageReference Include="K1Soft.IT.VectorTileHub.Server" Version="1.0.0" />
     <!-- pick exactly one provider (or more, to mix data sources per layer) -->
     <PackageReference Include="K1Soft.IT.VectorTileHub.Providers.SqlServer" Version="1.0.0" />
   </ItemGroup>
   ```

   Then `dotnet restore`. Available packages in the feed (all `1.0.0`):
   `…Server`, `…Providers.SqlServer`, `…Providers.Oracle`, `…Providers.Postgis`, and the transitive
   `…Abstractions` / `…Core` / `…AspNetCore` / `…Jobs` / `…Storage` (you don't reference these directly).

2. **Configure `appsettings.json`** — a `ConnectionStrings` entry plus a `VectorTileHub` block.
   See [docs/configuration.md](../../../docs/configuration.md) and Appendix C of the tutorial.

3. **Define layers** as JSON files in `LayerConfigFolder` (default `VectorTileHub/Layers`),
   one file per layer. Minimum: `id`, `provider` (type/connection/table/geometry/sourceSrid),
   `tile` (zoom range, extent=4096), `attributes.include` (whitelist), optional `extent`,
   `cacheRules` (variants), `cache`.

4. **Register services in `Program.cs`** (services only — you still own controllers & auth):

   ```csharp
   using K1Soft.IT.VectorTileHub;                     // AddVectorTileHubServer
   using K1Soft.IT.VectorTileHub.AspNetCore;          // UseVectorTileHubHangfireDashboard
   using K1Soft.IT.VectorTileHub.Providers.SqlServer; // AddVectorTileHubSqlServerProvider
   using K1Soft.IT.VectorTileHub.Storage;             // EnsureVectorTileHubStorageAsync

   builder.Services.AddControllers();                 // host owns MVC
   builder.Services.AddAuthentication(/* your scheme */);
   builder.Services.AddAuthorization();

   builder.Services.AddVectorTileHubServer(builder.Configuration); // core+storage+jobs
   builder.Services.AddVectorTileHubSqlServerProvider();           // one per data tech

   var app = builder.Build();
   await app.Services.EnsureVectorTileHubStorageAsync();           // MUST run once at startup

   app.UseAuthentication();
   app.UseAuthorization();
   app.MapControllers();                                  // your controllers (step 5)
   app.MapHealthChecks("/vector-tile-hub/health");        // optional
   app.UseVectorTileHubHangfireDashboard(/* auth filter */); // optional, host-secured
   app.Run();
   ```

5. **Write controllers** that call the three services. Copy the reference controllers from
   §8 of the tutorial (`TilesController`, `LayersController`, `CacheAdminController`) — the Sample
   project ships working versions. Map `VectorTileResult.Status` to HTTP results:
   `Ok`→`File(bytes, contentType)`, `NoContent`→204, `BadRequest`/`NotFound`/`ServiceUnavailable`
   accordingly. Set `X-VTH-From-Cache` / `X-VTH-Stale` from the result.

6. **Secure it** (your job): tiles usually anonymous or `[Authorize]`; admin/config controllers
   `[Authorize(Roles=...)]`; Hangfire dashboard via an `IDashboardAuthorizationFilter` (default
   permits local requests only).

## Providers

Register the provider, then point each layer at it via `provider.type` (**case-sensitive keyed name**):

| Provider | Registration call | `provider.type` | NuGet |
|---|---|---|---|
| SQL Server | `AddVectorTileHubSqlServerProvider()` | `"SqlServer"` | `Microsoft.Data.SqlClient` |
| Oracle | `AddVectorTileHubOracleProvider()` | `"Oracle"` | `Oracle.ManagedDataAccess.Core` |
| PostGIS | `AddVectorTileHubPostgisProvider()` | `"Postgis"` | `Npgsql` |

You may register multiple providers; each layer picks one. `sourceSrid: 4326` is reprojected
4326→3857 automatically; `3857` is used as-is. **Stored geometry SRID must match `sourceSrid`.**

## Variants (filtered tiles)

A variant is an independently cached, separately addressable filtered view. The library is
**role-agnostic** — the host maps a user/role to a `variantKey` and passes it to `GetTileAsync`.
Define in `cacheRules` with a `filter` (`operator` ∈ `Equals`/`In`/`NotEquals`/`IsNull`/`IsNotNull`).
Values are always bound as parameters; identifiers validated `[A-Za-z0-9_]`. Serve via
`?variant=residential`.

## Caching & pre-generation

Tiles enter the cache **on-demand** (cache miss when `tile.allowOnDemandGeneration=true`) or via
**Hangfire pre-generation** (`IVectorTileCacheAdmin.EnqueueGenerate(...)`). Admin ops:
`EnqueueGenerate`, `EnqueueDelete`, `EnqueueNotifyChange`, `EnqueueSwap` (blue/green),
`InvalidateAsync` (synchronous bbox removal), `GetStatusAsync`. Recommended strategy: set
`layer.extent` to real data bounds (enables fast in-memory R-tree + extent gating), cap deep zooms
with `tile.maxGenerationZoom`, pre-generate with a sensible `maxDegreeOfParallelism`, use
`EnqueueNotifyChange`/`InvalidateAsync` for incremental updates. `cache.refreshPeriodMinutes>0`
enables stale-while-revalidate (0 = never stale).

## Frontend (OpenLayers + ol-mapbox-style)

Fetch layer metadata from your `/layers/{id}` endpoint; point a GL-style vector `source.tiles` at
your tile route `"/vector-tile-hub/tiles/{id}/{z}/{x}/{y}.pbf"`; clamp `source.minzoom`/`maxzoom`
to the layer's range so the renderer **over-zooms** past `maxZoom` instead of requesting missing tiles.

## Troubleshooting (most common)

- **Tiles slow / time out** → missing or unused **spatial index**. Build one bounded to your data
  extent and confirm the optimizer uses it. Raising `commandTimeoutSeconds` is only a stopgap.
- **Unexpected `204 No Content`** → coordinate outside `layer.extent` (extent gating) or outside
  min/maxZoom. Check the extent SRID.
- **"Layer not found"** → `provider.type` doesn't match a registered provider key (case-sensitive),
  or `AddVectorTileHub<Db>Provider()` was not called.
- **Map blank above a zoom** → set the GL **source** `maxzoom` to the layer's `maxZoom`.
- **Jobs vanish after restart** → Hangfire uses in-memory storage by default.

## Authoritative references (read for full detail)

- **`docs/developer-tutorial.md`** — full end-to-end guide; Appendix A (service/model reference),
  Appendix B (layer schema), Appendix C (`VectorTileHubOptions`). **Start here.**
- `docs/configuration.md`, `docs/architecture.md`, `docs/cache-operations.md`,
  `docs/provider-setup.md`, `docs/sample-map.md`.
- `src/K1Soft.IT.VectorTileHub.Sample` — canonical reference host (real controllers + map viewer).
- `src/K1Soft.IT.VectorTileHub.Abstractions/Interfaces` & `/Models` — the exact public contracts.
