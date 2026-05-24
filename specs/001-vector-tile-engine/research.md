# Research: VectorTileHub — Reusable Vector Tile Engine

**Date**: 2026-05-24
**Branch**: `001-vector-tile-engine`

## R1: MVT/PBF Encoding in .NET

**Decision**: Use NetTopologySuite.IO.VectorTiles.Mapbox for MVT
encoding.

**Rationale**: NTS is already the geometry library for the project.
Its MVT extension writes Mapbox Vector Tile format directly from
NTS Geometry objects, avoiding manual protobuf serialization. The
encoder accepts features with geometry + attributes and produces
valid PBF bytes. It supports configurable extent, buffer, and
geometry clipping.

**Alternatives considered**:
- Custom protobuf implementation (Google.Protobuf + MVT .proto
  schema) — full control but significant effort to handle geometry
  simplification, coordinate transformation to tile-local space,
  and protobuf tag encoding correctly. Reserve as fallback if NTS
  MVT package proves too limited.
- MapboxTileCS — lightweight but less maintained and does not
  integrate with NTS geometry model directly.

## R2: Spatial Query Patterns for Providers

**Decision**: Providers execute parameterized bounding-box spatial
queries via ADO.NET and return WKB (Well-Known Binary) bytes.
NTS `WKBReader` parses WKB into Geometry objects in the core layer.

**Rationale**: WKB is a compact, standardized binary format supported
natively by both SQL Server (`geometry.STAsBinary()`) and Oracle
(`SDO_UTIL.TO_WKBGEOMETRY()`). Returning WKB keeps providers
decoupled from NTS internals — providers just return byte arrays
and attribute dictionaries.

**SQL Server spatial query pattern**:
```sql
SELECT [IdColumn], [Geom].STAsBinary() AS GeomWkb, [Attr1], [Attr2]
FROM [Schema].[Table]
WHERE [Geom].STIntersects(
    geometry::STGeomFromWKB(@envelope, @srid)
) = 1
```

**Oracle spatial query pattern**:
```sql
SELECT "ID_COLUMN", SDO_UTIL.TO_WKBGEOMETRY("GEOM") AS GEOM_WKB,
       "ATTR1", "ATTR2"
FROM "SCHEMA"."TABLE"
WHERE SDO_RELATE("GEOM",
    SDO_GEOMETRY(2003, :srid, NULL,
        SDO_ELEM_INFO_ARRAY(1, 1003, 3),
        SDO_ORDINATE_ARRAY(:minx, :miny, :maxx, :maxy)),
    'mask=ANYINTERACT') = 'TRUE'
```

**Alternatives considered**:
- Returning NTS Geometry directly from providers — creates a hard
  dependency on NTS within provider packages. WKB is more portable.
- Returning GeoJSON text — verbose and slow to parse compared to WKB.

## R3: Tile Math (XYZ to Bounds)

**Decision**: Implement a static `TileCoordinateUtils` class with
standard formulas for XYZ-to-EPSG:3857 bounding box conversion.

**Rationale**: The formulas are well-known and compact. No external
library needed.

**Key formulas**:
- Tile to lon/lat: standard Slippy Map math
  (`lon = x / 2^z * 360 - 180`,
   `lat = atan(sinh(π - 2π*y/2^z)) * 180/π`)
- Lon/lat to Web Mercator (EPSG:3857):
  (`mx = lon * 20037508.34 / 180`,
   `my = ln(tan((90+lat)*π/360)) * 20037508.34 / π`)
- Buffer expansion: extend envelope by `buffer/extent * tileWidth`
  in map units
- Coverage calculation: given a bounding box and zoom range, compute
  all tile coordinates that intersect (for cache generation and
  invalidation)

**Alternatives considered**:
- ProjNet4GeoAPI for coordinate transformations — overkill for the
  two fixed projections (EPSG:4326 ↔ EPSG:3857) used here.

## R4: Cache Disk Structure

**Decision**: Use a hierarchical folder structure:
```
{CacheRoot}/{LayerId}/{ScopeKey}/{CacheVersion}/{z}/{x}/{y}.pbf
```

**Rationale**: Including all five dimensions (layer, scope, version,
z, x/y) in the path ensures:
- Scope isolation (different scopes never share cached tiles)
- Version isolation (cache swap creates a new version folder;
  old version folder can be deleted as a unit)
- Efficient bulk deletion (delete one folder to clear an entire
  version or scope)
- Filesystem-friendly distribution (z/x/y splits files across
  directories to avoid single-directory bottlenecks)

**Cache key composite** (for memory cache and lookup):
`{LayerId}:{ScopeKey}:{CacheVersion}:{z}:{x}:{y}`

**Alternatives considered**:
- Flat file naming (`{LayerId}_{scope}_{version}_{z}_{x}_{y}.pbf`)
  — single directory with millions of files is slow on most
  filesystems.
- Database-backed cache (store bytes in SQLite/SQL Server) —
  higher latency and storage overhead than raw filesystem for
  binary blobs. May be suitable for distributed cache in a future
  version.

## R5: Hangfire Integration

**Decision**: Use Hangfire for background job scheduling with the
host's choice of storage backend.

**Rationale**: Hangfire is the de facto .NET background job library.
It provides fire-and-forget jobs (`BackgroundJob.Enqueue`),
continuation chains, dashboard UI with authorization, and
persistence via pluggable storage (SQL Server, SQLite, Redis).

**Integration pattern**:
- Library registers job classes via DI
- Library exposes `app.UseVectorTileHubHangfireDashboard()` for
  dashboard mounting with configurable auth
- Admin endpoints enqueue jobs via `IBackgroundJobClient`
- Job classes depend on core services (cache, settings, providers)
  via constructor injection
- Cache swap is a continuation chain: create folder → switch
  settings → enqueue generation → enqueue old-cache deletion

**Alternatives considered**:
- Quartz.NET — more complex configuration, heavier, less common in
  ASP.NET Core ecosystem for simple fire-and-forget patterns.
- Native `IHostedService` / `BackgroundService` — no persistence,
  no dashboard, no retry. Insufficient for durable cache workflows.

## R6: Runtime Settings Store

**Decision**: Use EF Core with SQLite as the default durable store
for runtime settings. Expose an abstraction
(`IVectorTileRuntimeSettingsStore`) so hosts can swap to SQL Server
or another store.

**Rationale**: SQLite requires no external database server, works
cross-platform, and is sufficient for single-server deployment.
EF Core provides migrations and a clean data access pattern for
the low-volume settings operations (not hot-path).

**Schema** (single table):
```
LayerRuntimeSettings
├── LayerId (PK, int)
├── ActiveCacheVersion (string)
├── CacheGenerationStatus (string enum: Idle/Running/Failed)
├── CacheGenerationJobId (string, nullable)
├── LastGenerationStartedAt (DateTimeOffset, nullable)
├── LastGenerationCompletedAt (DateTimeOffset, nullable)
├── LastInvalidatedAt (DateTimeOffset, nullable)
├── UpdatedAt (DateTimeOffset)
└── Metadata (string, nullable — JSON blob for extensibility)
```

**Alternatives considered**:
- Raw SQLite via Dapper — lighter but loses migration support and
  requires manual schema management.
- JSON file on disk — not safe for concurrent access from background
  jobs and request threads.

## R7: Security Scope Resolution

**Decision**: Implement a default `IVectorTileSecurityScopeResolver`
that maps `ClaimsPrincipal` roles to scope keys using a configurable
role-to-scope mapping in layer config. Hosts can replace with a
custom implementation.

**Rationale**: The most common pattern is role-based access (admin,
engineer, public). A simple claim-to-scope mapping covers this
without coupling to any specific identity provider.

**Default resolution logic**:
1. If layer has no security rules and global default is "public",
   return a well-known "public" scope
2. If layer has no security rules and global default is "require-auth",
   require authenticated principal (return 401 if anonymous)
3. If layer has security rules, match user roles/claims against the
   layer's scope mapping to determine the scope key
4. Scope key becomes part of the cache key and is passed to the
   provider for server-side filtering

**Alternatives considered**:
- Policy-based authorization via ASP.NET Core authorization
  middleware — too coarse (layer-level auth is needed, not just
  endpoint-level). The scope resolver works alongside standard auth.

## R8: Health Check Integration

**Decision**: Implement `IHealthCheck` that verifies: (1) internal
settings store is reachable, (2) cache root folder is accessible
and writable, (3) layer config folder is readable.

**Rationale**: These three checks cover the library's own
infrastructure. Provider health (database connectivity) is the
host's responsibility since connection strings are host-managed.

**Alternatives considered**:
- Include provider connectivity checks — would require each provider
  to implement a health check interface and the library to aggregate
  them. Viable future enhancement but out of scope for v1.
