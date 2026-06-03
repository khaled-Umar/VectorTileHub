# VectorTileHub — Developer Tutorial

A complete, end-to-end guide to consuming **VectorTileHub** in your own ASP.NET Core
application: serving cache-aware [Mapbox Vector Tiles](https://docs.mapbox.com/data/tilesets/guides/vector-tiles-introduction/)
(MVT/PBF) from a spatial database (SQL Server, Oracle, or PostGIS).

> **Audience:** .NET developers integrating VectorTileHub into a host app.
> **Target framework:** .NET 10. **Root namespace:** `K1Soft.IT.VectorTileHub`.

---

## Table of contents

1. [Mental model: what the library does (and doesn't) do](#1-mental-model)
2. [Architecture & projects](#2-architecture--projects)
3. [Prerequisites](#3-prerequisites)
4. [Step 1 — Reference the library](#4-step-1--reference-the-library)
5. [Step 2 — Configure (`appsettings.json`)](#5-step-2--configure-appsettingsjson)
6. [Step 3 — Define a layer](#6-step-3--define-a-layer)
7. [Step 4 — Register services (`Program.cs`)](#7-step-4--register-services-programcs)
8. [Step 5 — Expose endpoints with your own controllers](#8-step-5--expose-endpoints-with-your-own-controllers)
9. [Step 6 — Secure it](#9-step-6--secure-it)
10. [Step 7 — Run & verify](#10-step-7--run--verify)
11. [Database providers](#11-database-providers)
12. [Caching & background jobs](#12-caching--background-jobs)
13. [Variants (filtered caches)](#13-variants-filtered-caches)
14. [Zoom ranges & client over-zoom](#14-zoom-ranges--client-over-zoom)
15. [Frontend integration (OpenLayers)](#15-frontend-integration-openlayers)
16. [Health checks](#16-health-checks)
17. [Appendix A — Service & model reference](#17-appendix-a--service--model-reference)
18. [Appendix B — Layer config schema reference](#18-appendix-b--layer-config-schema-reference)
19. [Appendix C — `VectorTileHubOptions` reference](#19-appendix-c--vectortilehuboptions-reference)
20. [Troubleshooting & performance](#20-troubleshooting--performance)

---

## 1. Mental model

VectorTileHub is a **service library, not a framework**. The single most important thing to
understand:

> **The library exposes *no* HTTP endpoints.** It registers injectable **services**; *your* app
> owns the HTTP surface — you write the controllers, choose the routes, and apply your own
> authentication/authorization.

This is deliberate: tile and cache-admin endpoints are exactly the things a host needs to control
(who can read which layer, whether admin/cache routes exist at all, how they're secured). The
library gives you three services and you decide what to surface:

| Service | What it does |
|---|---|
| `IVectorTileService` | Produces an MVT/PBF tile for `(layerId, z, x, y, variant)`. |
| `IVectorTileLayerConfigProvider` | Reads layer metadata (zoom range, extent, variants) and reloads config from disk. |
| `IVectorTileCacheAdmin` | Cache operations: generate / delete / invalidate / notify-change / swap / status. |

The library performs **no authentication or authorization** — that is 100% the host's job. Where a
filtered ("variant") tile is wanted, the host maps the caller's identity/role to a *variant key*
and passes it to `GetTileAsync`.

> **Convention used throughout this repo:** HTTP endpoints are **MVC controllers**
> (`[ApiController]` + `ControllerBase` + `[HttpGet]`/`[HttpPost]`, mapped via
> `app.MapControllers()`), never minimal APIs.

---

## 2. Architecture & projects

```
Your Host App  (you write controllers here)
 ├─ K1Soft.IT.VectorTileHub.Server          ← facade: one call registers the whole stack
 │    └─ AspNetCore ─ Core ─ Storage ─ Jobs ─ Abstractions   (all transitive)
 └─ K1Soft.IT.VectorTileHub.Providers.<Db>  ← your chosen DB provider (pluggable)
```

| Project | Responsibility |
|---|---|
| `…Abstractions` | Public contracts & models (`IVectorTileService`, `IVectorTileCacheAdmin`, `VectorTileLayerConfig`, `VectorTileResult`, …). |
| `…Core` | Orchestration (`VectorTileOrchestrator`), tile math, MVT encoding, variant resolution, JSON layer-config loading. |
| `…Storage` | SQLite runtime-settings store (cache versions, generation status) via EF Core. |
| `…Jobs` | Hangfire background jobs + `IVectorTileCacheAdmin`. |
| `…AspNetCore` | Host integration helpers (service registration, Hangfire dashboard middleware, health check). **Ships no controllers.** |
| `…Server` | Facade that wires Core + Storage + Jobs in one call. **Provider-agnostic.** |
| `…Providers.SqlServer` / `.Oracle` / `.Postgis` | Spatial data providers (pluggable). |
| `…Sample` | Reference host app — shows the controllers and a map viewer. |

The provider model mirrors EF Core: you reference the **core** (`.Server`) plus a **provider**
package, exactly like `EFCore` + `EFCore.SqlServer`.

---

## 3. Prerequisites

- **.NET 10 SDK**
- A spatial database with a geometry column and (strongly recommended) a **spatial index**:
  - **SQL Server** 2016+ (`geometry`/`geography`), or
  - **Oracle** with Oracle Spatial (`SDO_GEOMETRY`), or
  - **PostgreSQL** with the **PostGIS** extension.
- Source geometries in **EPSG:4326** (lon/lat) or **EPSG:3857** (Web Mercator). VectorTileHub
  serves tiles in Web Mercator and reprojects 4326 → 3857 automatically.

---

## 4. Step 1 — Reference the library

Reference the **facade** plus **one provider**. Two project (or package) references total:

```xml
<ItemGroup>
  <!-- The facade brings Core/Storage/Jobs/AspNetCore/Abstractions transitively. -->
  <ProjectReference Include="..\K1Soft.IT.VectorTileHub.Server\K1Soft.IT.VectorTileHub.Server.csproj" />
  <!-- Pick exactly one provider (or more, if you mix data sources per layer). -->
  <ProjectReference Include="..\K1Soft.IT.VectorTileHub.Providers.SqlServer\K1Soft.IT.VectorTileHub.Providers.SqlServer.csproj" />
</ItemGroup>
```

Your host project should use the Web SDK (`<Project Sdk="Microsoft.NET.Sdk.Web">`) so MVC and the
endpoint-routing types are available.

---

## 5. Step 2 — Configure (`appsettings.json`)

Two sections: a **connection string** for your spatial DB and the **`VectorTileHub`** options
block.

```jsonc
{
  "ConnectionStrings": {
    "Default": "Server=127.0.0.1;Database=GisDb;User ID=sa;Password=***;TrustServerCertificate=True"
  },
  "VectorTileHub": {
    "Enabled": true,
    "RoutePrefix": "/vector-tile-hub",        // advisory only — YOU choose the actual routes
    "DefaultServingSrid": 3857,
    "DefaultTileExtent": 4096,                 // MVT grid resolution per tile (not geographic)
    "DefaultTileBuffer": 64,
    "LayerConfigFolder": "VectorTileHub/Layers",
    "DefaultCacheRootFolder": "C:\\Temp\\VectorTileHub\\Cache",
    "UseResponseCompression": true,
    "UseMemoryCache": true,
    "UseDiskCache": true,
    "HealthCheckPath": "/vector-tile-hub/health",
    "InternalSettingsStore": {
      "Provider": "Sqlite",
      "ConnectionString": "Data Source=C:\\Temp\\VectorTileHub\\vector_tile_hub.db"
    },
    "Hangfire": {
      "Enabled": true,
      "DashboardPath": "/vector-tile-hub/jobs"
    }
  }
}
```

See [Appendix C](#19-appendix-c--vectortilehuboptions-reference) for every option.

> **Note on `RoutePrefix`:** because the host now owns the HTTP surface, `RoutePrefix` is
> *advisory* — it no longer auto-routes anything. Use it as the base path in your own controller
> routes if you like, or ignore it.

---

## 6. Step 3 — Define a layer

Layers are JSON files discovered from `LayerConfigFolder` (relative to the app content root) and/or
explicit `LayerConfigPaths`. Convention: one file per layer, e.g.
`VectorTileHub/Layers/82-parcels.json`.

```jsonc
{
  "id": 82,
  "layerKey": "parcels",
  "layerName": "Parcels Layer",
  "enabled": true,

  "provider": {
    "type": "SqlServer",                 // keyed provider name — matches your AddVectorTileHub<Db>Provider()
    "connectionStringName": "Default",   // or set "connectionString" inline
    "tableName": "[GisDb].[dbo].[Parcels]",
    "idColumn": "Id",
    "geometryColumn": "Geom",
    "sourceSrid": 4326,                  // 4326 (lon/lat) or 3857 (Web Mercator)
    "commandTimeoutSeconds": 120         // optional; null = ADO.NET default (30s), 0 = no timeout
  },

  "tile": {
    "minZoom": 12,
    "maxZoom": 17,
    "maxGenerationZoom": 17,             // optional cap for cache GENERATION only (serving honors maxZoom)
    "extent": 4096,                      // MVT grid resolution
    "buffer": 64,
    "clipGeometry": true,
    "returnEmptyTileOutsideZoomRange": true,
    "allowOnDemandGeneration": true
  },

  "extent": {                            // optional geographic bounds — see §12
    "minX": 38.93, "minY": 21.40,
    "maxX": 39.56, "maxY": 22.36,
    "srid": 4326
  },

  "attributes": {
    "include": [ "PARCELNUMBER", "LAND_USES", "DISTRICT" ]
  },

  "cacheRules": [
    { "variantKey": "default", "isDefault": true },
    {
      "variantKey": "residential",
      "displayName": "Residential land uses only",
      "filter": { "column": "LAND_USES", "operator": "In", "values": [ "Villa", "Housing" ] }
    }
  ],

  "cache": {
    "enabled": true,
    "refreshPeriodMinutes": 1440          // stale-while-revalidate window; 0 = never stale
  }
}
```

Full field documentation is in [Appendix B](#18-appendix-b--layer-config-schema-reference).

---

## 7. Step 4 — Register services (`Program.cs`)

```csharp
using K1Soft.IT.VectorTileHub;                       // AddVectorTileHubServer
using K1Soft.IT.VectorTileHub.AspNetCore;            // UseVectorTileHubHangfireDashboard
using K1Soft.IT.VectorTileHub.Providers.SqlServer;   // AddVectorTileHubSqlServerProvider
using K1Soft.IT.VectorTileHub.Storage;               // EnsureVectorTileHubStorageAsync

var builder = WebApplication.CreateBuilder(args);

// 1) The host owns MVC. AddControllers() registers your controllers (and the API explorer Swagger uses).
builder.Services.AddControllers();

// 2) Your own auth — the library enforces none.
builder.Services.AddAuthentication(/* your scheme */);
builder.Services.AddAuthorization();

// 3) Register the VectorTileHub services (core + storage + jobs). Provider-agnostic.
builder.Services.AddVectorTileHubServer(builder.Configuration);

// 4) Register exactly the provider(s) your layers use.
builder.Services.AddVectorTileHubSqlServerProvider();
//  .AddVectorTileHubOracleProvider();
//  .AddVectorTileHubPostgisProvider();

var app = builder.Build();

// 5) Create/upgrade the internal SQLite settings store and warm its in-memory mirror.
await app.Services.EnsureVectorTileHubStorageAsync();

// 6) Host-owned pipeline — YOU decide what is exposed.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();                                  // your tile/layer/admin controllers (Step 5)
app.MapHealthChecks("/vector-tile-hub/health");        // optional
app.UseVectorTileHubHangfireDashboard(/* auth filter */); // optional, opt-in, host-secured

app.Run();
```

Key points:

- `AddVectorTileHubServer(IConfiguration)` = `AddVectorTileHub` (core + storage + health-check
  registration) + `AddVectorTileHubJobs` (Hangfire + `IVectorTileCacheAdmin`). **Services only.**
- The **provider is separate and pluggable** — register one per data technology you use.
- `EnsureVectorTileHubStorageAsync()` must run once at startup before serving.

---

## 8. Step 5 — Expose endpoints with your own controllers

This is where you choose your routes and security. Below are reference controllers you can copy.
(The Sample project ships these as `TilesController`, `LayersController`, `CacheAdminController`,
`ConfigAdminController`.)

### 8.1 Tile endpoint

```csharp
using K1Soft.IT.VectorTileHub;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("vector-tile-hub")]
public sealed class TilesController : ControllerBase
{
    private readonly IVectorTileService _tiles;
    public TilesController(IVectorTileService tiles) => _tiles = tiles;

    [HttpGet("tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf")]
    public async Task<IActionResult> GetTile(int layerId, int z, int x, int y,
        [FromQuery] string? variant, CancellationToken ct)
    {
        // The host decides the variant. Honor an explicit ?variant=, or derive from identity, e.g.:
        //   variant = User.IsInRole("Resident") ? "residential" : null;
        var result = await _tiles.GetTileAsync(layerId, z, x, y, variant, ct);

        if (result.Status == VectorTileResultStatus.Ok)
        {
            Response.Headers["X-VTH-From-Cache"] = result.FromCache ? "true" : "false";
            Response.Headers["X-VTH-Stale"]      = result.IsStale   ? "true" : "false";
        }

        return result.Status switch
        {
            VectorTileResultStatus.Ok                 => File(result.TileBytes, result.ContentType),
            VectorTileResultStatus.NoContent          => NoContent(),                    // 204 — outside extent
            VectorTileResultStatus.BadRequest         => BadRequest(new { error = result.Error }),
            VectorTileResultStatus.NotFound           => NotFound(new { error = result.Error }),
            VectorTileResultStatus.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error }),
            _                                         => StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error ?? "Unexpected tile error" })
        };
    }
}
```

### 8.2 Layer metadata endpoint

The frontend needs each layer's zoom range and tile URL template.

```csharp
[ApiController]
[Route("vector-tile-hub")]
public sealed class LayersController : ControllerBase
{
    private readonly IVectorTileLayerConfigProvider _provider;
    public LayersController(IVectorTileLayerConfigProvider provider) => _provider = provider;

    [HttpGet("layers")]
    public IActionResult GetLayers() =>
        Ok(new { layers = _provider.GetAllLayers().Where(l => l.Enabled).Select(ToDto) });

    [HttpGet("layers/{layerId:int}")]
    public IActionResult GetLayer(int layerId)
    {
        var layer = _provider.GetLayer(layerId);
        return layer is null || !layer.Enabled ? NotFound() : Ok(ToDto(layer));
    }

    private static object ToDto(VectorTileLayerConfig l) => new
    {
        id = l.Id,
        layerName = l.LayerName,
        minZoom = l.Tile.MinZoom,
        maxZoom = l.Tile.MaxZoom,
        tileUrlTemplate = $"/vector-tile-hub/tiles/{l.Id}/{{z}}/{{x}}/{{y}}.pbf",
        variants = l.CacheRules.Count > 0
            ? l.CacheRules.Select(r => r.VariantKey).ToArray()
            : new[] { VectorTileVariant.DefaultKey },
        attributes = l.Attributes.Include
    };
}
```

### 8.3 Cache-admin endpoint (secured)

```csharp
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("vector-tile-hub/admin/layers")]
[Authorize(Roles = "GISAdmin")]                 // host-applied policy — the library enforces none
public sealed class CacheAdminController : ControllerBase
{
    private readonly IVectorTileCacheAdmin _admin;
    public CacheAdminController(IVectorTileCacheAdmin admin) => _admin = admin;

    [HttpPost("{layerId:int}/cache/generate")]
    public IActionResult Generate(int layerId, [FromBody] GenerateDto? r)
        => Accepted(new { jobId = _admin.EnqueueGenerate(layerId, r?.MinZoom, r?.MaxZoom, r?.Variants, r?.MaxDegreeOfParallelism) });

    [HttpGet("{layerId:int}/cache/status")]
    public async Task<IActionResult> Status(int layerId, CancellationToken ct)
        => await _admin.GetStatusAsync(layerId, ct) is { } s ? Ok(s) : NotFound();

    public sealed record GenerateDto(int? MinZoom, int? MaxZoom, string[]? Variants, int? MaxDegreeOfParallelism);
}
```

> The host defines the **HTTP request DTOs** (the JSON shape is your concern). The service methods
> take primitive parameters — see [Appendix A](#17-appendix-a--service--model-reference).

---

## 9. Step 6 — Secure it

Because you own the controllers and the dashboard mounting, you apply standard ASP.NET Core
security:

- **Tiles** — typically anonymous (public read) or `[Authorize]`. To serve *filtered* data per
  user, map the role to a variant key inside the controller and pass it to `GetTileAsync`.
- **Admin/config** — guard with `[Authorize(Roles = "…")]` or a policy. These trigger destructive
  cache operations.
- **Hangfire dashboard** — `UseVectorTileHubHangfireDashboard(...)` accepts
  `IDashboardAuthorizationFilter`s. With none, Hangfire's default permits **local requests only**.
  Supply your own to gate it:

```csharp
using Hangfire.Dashboard;

public sealed class DashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        context.GetHttpContext().User?.IsInRole("GISAdmin") == true;
}

// Program.cs
app.UseVectorTileHubHangfireDashboard(new DashboardAuthFilter());
```

---

## 10. Step 7 — Run & verify

```powershell
dotnet build VectorTileHub.slnx -c Debug
dotnet run --project src/K1Soft.IT.VectorTileHub.Sample
```

Then:

| Check | URL |
|---|---|
| Layer list | `GET /vector-tile-hub/layers` |
| Single layer metadata | `GET /vector-tile-hub/layers/82` |
| A tile | `GET /vector-tile-hub/tiles/82/14/9123/6321.pbf` |
| Filtered tile | `GET /vector-tile-hub/tiles/82/14/9123/6321.pbf?variant=residential` |
| Health | `GET /vector-tile-hub/health` |
| Jobs dashboard | `GET /vector-tile-hub/jobs` |
| Swagger | `GET /swagger` (lists *your* controllers only) |

A tile returns `200` with `Content-Type: application/x-protobuf` and `X-VTH-From-Cache` /
`X-VTH-Stale` headers. Coordinates outside the layer's configured `extent` return `204 No Content`.

---

## 11. Database providers

Register the provider, then point each layer at it via `provider.type`. The match is a
**case-sensitive keyed name**:

| Provider | Package call | `provider.type` | NuGet dependency |
|---|---|---|---|
| SQL Server | `AddVectorTileHubSqlServerProvider()` | `"SqlServer"` | `Microsoft.Data.SqlClient` |
| Oracle | `AddVectorTileHubOracleProvider()` | `"Oracle"` | `Oracle.ManagedDataAccess.Core` |
| PostGIS | `AddVectorTileHubPostgisProvider()` | `"Postgis"` | `Npgsql` |

You can register **multiple** providers; each layer independently picks one. All providers:

- read the connection string from `provider.connectionString` (inline) or
  `provider.connectionStringName` (looked up in `ConnectionStrings`);
- honor `provider.commandTimeoutSeconds`;
- emit each feature's geometry as **WKB**, decoded with NetTopologySuite;
- intersect the requested tile envelope against the geometry column using a spatial predicate.

### SRID handling

- `sourceSrid: 4326` — the query envelope (Web Mercator) is converted to lon/lat for the spatial
  predicate, and returned geometries are reprojected **4326 → 3857** for tiling.
- `sourceSrid: 3857` — used as-is (no reprojection).

The stored geometry's SRID **must match** `sourceSrid`.

### Spatial predicate per provider

| Provider | WHERE clause (essence) |
|---|---|
| SQL Server | `Geom.STIntersects(geometry::STGeomFromWKB(@envelope, @srid)) = 1` |
| Oracle | `SDO_RELATE(Geom, SDO_GEOMETRY(...), 'mask=ANYINTERACT') = 'TRUE'` |
| PostGIS | `ST_Intersects(geom, ST_MakeEnvelope(@minx,@miny,@maxx,@maxy,@srid))` |

> **PostGIS note:** variant filters cast the column to `::text` because PostgreSQL won't implicitly
> compare a non-text column to a text parameter. Functionally identical to the other providers.

### Spatial index is essential

Low-zoom tiles cover huge areas. **Without a spatial index, every tile triggers a table scan** and
queries time out. Create one and verify the optimizer uses it. Example (SQL Server, bound the grid
to your data extent):

```sql
CREATE SPATIAL INDEX idx_Spatial_Parcels ON [dbo].[Parcels]([Geom])
USING GEOMETRY_AUTO_GRID
WITH (BOUNDING_BOX = (38.9, 21.4, 39.6, 22.4), CELLS_PER_OBJECT = 16, DROP_EXISTING = ON);
```

PostGIS: `CREATE INDEX ON parcels USING GIST (geom);` · Oracle: create an `MDSYS.SPATIAL_INDEX`.

---

## 12. Caching & background jobs

VectorTileHub caches encoded tiles on disk (and optionally in memory). There are two ways tiles get
into the cache:

1. **On-demand** — a tile requested but not cached is generated, cached, and returned (when
   `tile.allowOnDemandGeneration = true`).
2. **Pre-generation** — a background Hangfire job renders a zoom range up front. Use
   `IVectorTileCacheAdmin.EnqueueGenerate(...)`.

### `IVectorTileCacheAdmin` operations

| Method | Effect |
|---|---|
| `EnqueueGenerate(layerId, minZoom?, maxZoom?, variants?, maxDegreeOfParallelism?)` | Background pre-generation. Returns the Hangfire job id. |
| `EnqueueDelete(layerId, cacheVersion?, deleteAllVersions)` | Background cache deletion. |
| `EnqueueNotifyChange(layerId, minX, minY, maxX, maxY, srid, variants?)` | Background refresh of tiles in a bbox. |
| `EnqueueSwap(layerId, newVersion?, regenerateAfterSwap, deleteOldVersion)` | Atomically swap the active cache version (blue/green). Returns `{ JobId, NewVersion }`. |
| `InvalidateAsync(layerId, minX, minY, maxX, maxY, srid, variants?, ct)` | **Synchronous** removal of cached tiles in a bbox. Returns `null` if the layer is unknown. |
| `GetStatusAsync(layerId, ct)` | Current runtime state (active version, generation status, timestamps). |

### Performance knobs

- **`maxDegreeOfParallelism`** — generation runs `Parallel.ForEachAsync` over tiles. Set this from
  your admin request to use multiple threads.
- **`tile.maxGenerationZoom`** — caps generation depth (high zooms dominate tile counts). Serving
  still honors `tile.maxZoom`; zooms above the cap are produced on demand.
- **`layer.extent`** — when set, generation is bounded to that area (and uses an in-memory R-tree
  built from a single full-layer query instead of one DB query per tile — far faster). When unset,
  generation falls back to a per-tile query over the whole world (slow; logged as a warning).
- **Extent gating** — when `layer.extent` is set, tile requests *outside* the extent short-circuit
  to `204 No Content` with **no database query**.

### Stale-while-revalidate

`cache.refreshPeriodMinutes > 0` serves a cached tile immediately even when older than the window,
and enqueues a background refresh (`X-VTH-Stale: true` on the response). `0` = tiles never go stale.

### Hangfire dashboard

Live job progress (with per-tile rate and ETA) is shown at `Hangfire.DashboardPath`
(default `/vector-tile-hub/jobs`). Mount it with `UseVectorTileHubHangfireDashboard(...)` and secure
it (see §9). Hangfire uses **in-memory** storage by default — jobs do not survive a restart.

---

## 13. Variants (filtered caches)

A *variant* is an independently cached, separately addressable filtered view of a layer. The
library is **role-agnostic**: the host maps a user/role to a `variantKey` and passes it to
`GetTileAsync`; the variant's server-side `filter` scopes the source rows.

Define variants in `cacheRules`:

```jsonc
"cacheRules": [
  { "variantKey": "default", "isDefault": true },
  { "variantKey": "residential",
    "filter": { "column": "LAND_USES", "operator": "In", "values": [ "Villa", "Housing" ] } }
]
```

Supported `filter.operator` values (`FilterOperator`):

| Operator | Predicate | `values` |
|---|---|---|
| `Equals` | `col = @v0` (or `IN(...)` if multiple) | 1+ |
| `In` | `col IN (@v0, @v1, …)` | 1+ |
| `NotEquals` | `col <> @v0` | 1 |
| `IsNull` | `col IS NULL` | — |
| `IsNotNull` | `col IS NOT NULL` | — |

Values are **always** bound as parameters — never concatenated into SQL. Identifiers are validated
(`[A-Za-z0-9_]`) and quoted per provider.

Serve a variant: `GET /…/tiles/82/14/x/y.pbf?variant=residential`. Each variant has its own cache
entries and its own slot in pre-generation (`EnqueueGenerate(..., variants: ["residential"])`).

---

## 14. Zoom ranges & client over-zoom

- `tile.minZoom` / `tile.maxZoom` — the zoom band the layer serves. Outside it (and when
  `returnEmptyTileOutsideZoomRange = true`) the server returns an empty tile.
- **Over-zoom past `maxZoom`** is a *client* concern: configure the map's GL-style **source**
  `maxzoom` to the layer's `maxZoom`. The renderer then keeps drawing the top-zoom tiles, scaled,
  when the user zooms in further — instead of requesting tiles that don't exist. See §15.

> `tile.extent` (e.g. `4096`) is the **MVT grid resolution within a single tile**, unrelated to
> geographic extent or zoom. Leave it at `4096` unless you have a specific reason.

---

## 15. Frontend integration (OpenLayers)

Using [OpenLayers](https://openlayers.org/) + [ol-mapbox-style](https://github.com/openlayers/ol-mapbox-style):

```js
// 1) Read layer metadata from YOUR endpoint.
const meta = await (await fetch('/vector-tile-hub/layers/82')).json();

// 2) Load a Mapbox GL style whose vector source points at YOUR tile route.
const style = await (await fetch('/ol-style.json')).json();

// 3) Clamp the source zoom range to the layer's min/max so the renderer OVER-ZOOMS
//    beyond maxZoom (keeps drawing top-zoom tiles scaled) instead of requesting missing tiles.
const src = style.sources.vth;            // your GL source name
if (Number.isFinite(meta.minZoom)) src.minzoom = meta.minZoom;
if (Number.isFinite(meta.maxZoom)) src.maxzoom = meta.maxZoom;

await olms.apply(map, style);             // ol-mapbox-style
```

The GL source's `tiles` URL is your tile route, e.g.
`"/vector-tile-hub/tiles/82/{z}/{x}/{y}.pbf"`. The Sample includes a working `wwwroot/app.js`,
`index.html`, and an SLD→GL-style converter (`dotnet run -- gen-style …`).

---

## 16. Health checks

`AddVectorTileHub` registers a health check named `VectorTileHub`. The host maps it:

```csharp
app.MapHealthChecks("/vector-tile-hub/health");
```

Returns `Healthy`/`Unhealthy` for liveness/readiness probes.

---

## 17. Appendix A — Service & model reference

### `IVectorTileService`
```csharp
Task<VectorTileResult> GetTileAsync(int layerId, int z, int x, int y, string? variantKey, CancellationToken ct);
```

### `IVectorTileLayerConfigProvider`
```csharp
VectorTileLayerConfig? GetLayer(int layerId);
VectorTileLayerConfig? GetLayerByKey(string layerKey);
IReadOnlyList<VectorTileLayerConfig> GetAllLayers();
Task ReloadAsync(CancellationToken ct);     // re-reads the layer JSON files from disk
```

### `IVectorTileCacheAdmin`
```csharp
string EnqueueGenerate(int layerId, int? minZoom, int? maxZoom, string[]? variants, int? maxDegreeOfParallelism);
string EnqueueDelete(int layerId, string? cacheVersion, bool deleteAllVersions);
string EnqueueNotifyChange(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? variants);
CacheSwapResult EnqueueSwap(int layerId, string? newVersion, bool regenerateAfterSwap, bool deleteOldVersion);
Task<CacheInvalidateResult?> InvalidateAsync(int layerId, double minX, double minY, double maxX, double maxY, int srid, string[]? variants, CancellationToken ct);
Task<VectorTileLayerRuntimeSettings?> GetStatusAsync(int layerId, CancellationToken ct);

record CacheSwapResult(string JobId, string NewVersion);
record CacheInvalidateResult(int TilesInvalidated, int[] ZoomLevelsAffected);
```

### `VectorTileResult`
| Member | Meaning |
|---|---|
| `Status` | `Ok` · `NoContent` · `BadRequest` · `Unauthorized` · `Forbidden` · `NotFound` · `ServiceUnavailable` |
| `TileBytes` | Encoded MVT/PBF bytes (when `Ok`). |
| `ContentType` | `application/x-protobuf`. |
| `IsEmpty` | True for empty/no-content tiles. |
| `FromCache` | Served from cache vs freshly generated. |
| `IsStale` | A stale tile was served and a background refresh enqueued. |
| `Error` | Error message for non-`Ok` statuses. |

---

## 18. Appendix B — Layer config schema reference

| Field | Type | Default | Notes |
|---|---|---|---|
| `id` | int | — | Unique layer id used in routes. |
| `layerKey` | string | — | Stable string key. |
| `layerName` | string | — | Display name. |
| `enabled` | bool | `true` | Disabled layers are hidden/served as not-found by your controllers. |
| **`provider`** | object | — | |
| `provider.type` | string | — | `SqlServer` · `Oracle` · `Postgis` (case-sensitive). |
| `provider.connectionString` | string? | — | Inline connection string. |
| `provider.connectionStringName` | string? | — | Name looked up in `ConnectionStrings`. |
| `provider.tableName` | string | — | Source table/view (schema-qualified ok). |
| `provider.idColumn` | string | `Id` | Feature id column. |
| `provider.geometryColumn` | string | `Geom` | Geometry column. |
| `provider.sourceSrid` | int | `3857` | `4326` or `3857`. |
| `provider.commandTimeoutSeconds` | int? | null | null = 30s default; 0 = no timeout. |
| **`tile`** | object | — | |
| `tile.minZoom` | int | `0` | |
| `tile.maxZoom` | int | `21` | Served maximum zoom. |
| `tile.maxGenerationZoom` | int? | null | Caps **generation** only. |
| `tile.extent` | int | — | MVT grid resolution (use `4096`). |
| `tile.buffer` | int | — | Tile edge buffer (px on the MVT grid). |
| `tile.clipGeometry` | bool | `true` | Clip geometry to tile + buffer. |
| `tile.returnEmptyTileOutsideZoomRange` | bool | `true` | Empty tile vs not-found outside the band. |
| `tile.allowOnDemandGeneration` | bool | `true` | Generate-and-cache on a cache miss. |
| **`extent`** | object? | null | Geographic bounds; enables extent gating + bounded generation. |
| `extent.minX/minY/maxX/maxY` | double | — | Corner coordinates. |
| `extent.srid` | int | `4326` | `4326` or `3857`. |
| **`attributes.include`** | string[] | `[]` | Columns emitted as feature properties. |
| **`cacheRules`** | array | `[]` | Variants (see §13). Empty = one unfiltered `default`. |
| **`cache.enabled`** | bool | `true` | |
| `cache.cacheRootFolder` | string? | null | Override the global cache folder. |
| `cache.refreshPeriodMinutes` | int | `0` | Stale-while-revalidate window; 0 = never stale. |

---

## 19. Appendix C — `VectorTileHubOptions` reference

Bound from the `VectorTileHub` config section.

| Option | Default | Notes |
|---|---|---|
| `Enabled` | `true` | Master switch. |
| `RoutePrefix` | `/vector-tile-hub` | Advisory only — host owns routing. |
| `DefaultServingSrid` | `3857` | Served tile SRID. |
| `DefaultTileExtent` | `4096` | Default MVT grid resolution. |
| `DefaultTileBuffer` | `64` | Default tile buffer. |
| `LayerConfigPaths` | `[]` | Explicit layer-file paths (anywhere on disk). |
| `LayerConfigFolder` | `VectorTileHub/Layers` | Folder scanned for `*.json` layer files. |
| `DefaultCacheRootFolder` | `VectorTileHub/Cache` | Disk cache root. |
| `UseResponseCompression` | `true` | |
| `UseMemoryCache` | `true` | In-memory tile cache layer. |
| `UseDiskCache` | `true` | On-disk tile cache layer. |
| `HealthCheckPath` | `/vector-tile-hub/health` | Path you map `MapHealthChecks` to. |
| `InternalSettingsStore.Provider` | `Sqlite` | Runtime-settings store backend. |
| `InternalSettingsStore.ConnectionString` | null | null = auto-create local SQLite db. |
| `Hangfire.Enabled` | `true` | Mount the dashboard or not. |
| `Hangfire.DashboardPath` | `/vector-tile-hub/jobs` | Dashboard route. |

---

## 20. Troubleshooting & performance

| Symptom | Likely cause / fix |
|---|---|
| Tile requests time out / very slow generation | **No spatial index**, or the optimizer ignores it. Build/rebuild it bounded to your data extent. Raise `provider.commandTimeoutSeconds` only as a stopgap. |
| `204 No Content` for tiles you expect data | The coordinate is **outside `layer.extent`** (extent gating), or outside `min/maxZoom`. Check the configured extent SRID. |
| Layer not found at runtime | `provider.type` doesn't match a registered provider key (case-sensitive), or you forgot `AddVectorTileHub<Db>Provider()`. |
| Map blank above a zoom level | Set the GL **source** `maxzoom` to the layer's `maxZoom` to enable client over-zoom (§14). |
| Many `TaskCanceledException` in logs | Usually benign client cancellations (`HttpContext.RequestAborted`). Genuine `Execution Timeout Expired` = the spatial-index problem above. |
| EF `…Database.Command` log spam | Set `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` in logging config. |
| Jobs vanish after restart | Hangfire uses in-memory storage by default. Swap to a durable Hangfire storage if you need persistence. |
| Build error `MSB3027/MSB3021 … file is being used` | The host app is **running** and locking its `bin` dlls. Stop it, then rebuild. |

### Generation strategy (recommended)

1. Set `layer.extent` to the real data bounds (enables the fast in-memory R-tree path + gating).
2. Cap deep zooms with `tile.maxGenerationZoom`; let the deepest zooms generate on demand.
3. Pre-generate with a sensible `maxDegreeOfParallelism` and watch progress in the Hangfire dashboard.
4. Use `EnqueueNotifyChange` / `InvalidateAsync` for incremental updates after source-data edits;
   use `EnqueueSwap` for full blue/green rebuilds.

---

*This document reflects the current **service-only** architecture (the library exposes no HTTP
endpoints; the host owns controllers). The Sample project is the canonical reference host.*
