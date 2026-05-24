# VectorTileHub Specification Seed

Use this file as the seed prompt for Speckit to generate an implementation-ready specification.

## Role

Act as a senior .NET platform architect and GIS backend designer.

Your task is to produce a complete technical specification for a reusable ASP.NET Core library named **VectorTileHub** under the namespace:

```csharp
K1Soft.IT.VectorTileHub
```

## Primary Objective

Design a reusable ASP.NET Core C# library that adds Mapbox Vector Tile capabilities to any ASP.NET Core host application.

The library must expose configurable endpoints such as:

```http
GET /vector-tile-hub/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf
```

The result must be a specification for implementation, not marketing copy and not a product brief.

## What Speckit Must Produce

Generate a specification that is:

- Implementation-oriented
- Explicit about responsibilities and boundaries
- Safe by default
- Extensible for future providers
- Optimized for high-volume tile serving
- Clear enough that a development team can implement it without guessing major behavior

The generated specification must include:

1. System overview and goals
2. Scope and non-goals
3. Architecture and project/module boundaries
4. Configuration model
5. Domain model and key interfaces
6. Request lifecycle for tile generation and tile retrieval
7. Security model and scope resolution
8. Cache model, cache keys, cache invalidation, and cache replacement strategy
9. Data provider abstraction and provider-specific requirements
10. MVT/PBF encoding requirements
11. Background jobs and operational workflows
12. Public and admin API contracts
13. Performance, cost-control, and safety constraints
14. Acceptance criteria
15. Open assumptions and implementation notes where necessary

## Writing Rules

- Prefer precise requirements over narrative explanation.
- Use normative language: `must`, `must not`, `should`, `may`.
- Do not invent unnecessary product features.
- If something is ambiguous, choose a pragmatic default and record it as an explicit assumption.
- Preserve separation of concerns.
- Do not collapse the library into a monolithic hardcoded application design.
- Do not assume a single database engine.
- Do not rely on client-side filtering for security.
- Do not place hot-path design on EF Core entity tracking.

## Product Identity

- Product name: `VectorTileHub`
- Namespace: `K1Soft.IT.VectorTileHub`
- Type: reusable infrastructure library for ASP.NET Core
- Primary purpose: serve secure, cache-aware, provider-agnostic vector tiles

## Expected Host Integration

The specification should assume usage like:

```csharp
builder.Services.AddVectorTileHub(builder.Configuration);

app.MapVectorTileHubEndpoints();
app.UseVectorTileHubHangfireDashboard();
```

The host application must be able to configure:

- Global VectorTileHub settings
- Route prefix
- Layer configuration folder
- Cache root folders
- Internal settings database
- Background job settings
- Authorization policies
- Dashboard authorization
- Provider registrations and provider options

## Sample Application Requirement

The generated specification must also require a sample ASP.NET Core application that consumes the produced `VectorTileHub` library.

This sample application is not the main product. Its purpose is to demonstrate real integration, configuration, tile serving, and provider wiring using the reusable library.

The sample application must:

- Reference and use the generated `VectorTileHub` library
- Use SQL Server as the provider
- Connect using this connection string in sample configuration:

```json
{
  "ConnectionStrings": {
    "Default": "Server=127.0.0.1;Database=UALSDb;User ID=sa;Password=asdAAA123;TrustServerCertificate=True;Connection Timeout=3600"
  }
}
```

- Use this source table:

```sql
[UALSDb].[ualsdataview].[LayerData_82]
```

- Treat `Geom` as the geometry column
- Expose a layer configuration for this table through the library
- Include a minimal frontend or API-consumable setup sufficient to verify tile serving

The specification must make clear that the sample should not expose all table attributes in the vector tile output.

The sample should include only a small, intentional whitelist of fields needed for rendering and lightweight inspection, for example:

- `Id`
- `LayerId`
- `DISTRICT`
- `SUBMUNICIPALITY`
- `SUBDISTRICT_NAME`
- `PARCELNUMBER`
- `PARCEL_LANDUSE`
- `LAND_USES`
- `PLAN_NUMBER`
- `BlockNumber`

The specification should explicitly state that fields such as audit columns, concurrency fields, long text blobs, internal identifiers not needed by the map, and other non-rendering metadata should be excluded from MVT output by default.

The specification may reference this source query only for schema understanding:

```sql
SELECT TOP (1000)
    [fid],
    [Id],
    [Geom],
    [LayerId],
    [SUBMUNICIPALITY],
    [DISTRICT],
    [SUBDISTRICT_NUMBER],
    [SUBDISTRICT_NAME],
    [PLAN_DIVISION_NO],
    [PLAN_CODING_NO],
    [CODING_PLAN],
    [PLAN_PART_NO],
    [PLAN_NUMBER],
    [Other],
    [BlockNumber],
    [PARCELNUMBER],
    [PARCEL_LANDUSE],
    [PARCEL_TYPE],
    [CODING_LANDUSE],
    [NOTES_LANDUSE],
    [LAND_USES],
    [FLOORS_NUMBER],
    [CONSTRUCTION_RATIO],
    [FAR],
    [UPPER_APPENDIX],
    [UPPER_APPENDIX_RATIO],
    [NOTES_BUILDSYSTEM],
    [BUILDING_REGULATION],
    [ROAD_TYPE],
    [SERVICE_NAME],
    [FRONT_SETBACKS],
    [SIDE_SETBACKS],
    [BACKGROUND_SETBACKS],
    [STREET_SETBACKS],
    [Minimum_Base_Floor_Height],
    [Maximum_Base_Floor_Height],
    [Annex_Floor_Height],
    [PARKING_NUMBER],
    [PLAN_USE],
    [DEFINITION],
    [NOTES],
    [Type_t],
    [Back_Widget],
    [Side_or_back_street],
    [IDSYS],
    [DocumentCode],
    [SHAPE_Length],
    [SHAPE_Area],
    [ExtraProperties],
    [ConcurrencyStamp],
    [CreationTime],
    [CreatorId],
    [LastModificationTime],
    [LastModifierId],
    [IsDeleted],
    [DeleterId],
    [DeletionTime]
FROM [UALSDb].[ualsdataview].[LayerData_82];
```

The specification should instruct the implementation to use only a curated subset of these columns for tile payloads.

## Main Design Principles

VectorTileHub must be designed as a policy-driven vector tile engine.

It must not merely query a database and encode bytes.

For every request, the system should explicitly decide:

- Who is requesting
- Which layer is being requested
- Which zoom/x/y tile is requested
- Which security scope applies
- Which provider should serve the layer
- Whether cached output is valid
- Which cache version/folder is active
- Whether to return a cached tile, generate a tile, return an empty tile, or enqueue background work

## Core Functional Requirements

The library must support:

- Dynamic tile generation
- Disk cache
- Optional in-memory cache
- Optional future distributed cache support without redesigning the core
- Role/scope-aware tile caching
- Background cache generation
- Cache deletion
- Cache invalidation
- Safe two-stage cache replacement
- Extensible data providers
- Config-driven layer definitions
- Secure server-side data filtering

## Core Concepts

### Layer

A layer represents a GIS dataset that can be served as MVT/PBF tiles.

Each layer must define:

- Unique integer ID
- Unique layer key
- Display name
- Enabled flag
- Provider type
- Provider-specific connection settings
- Geometry column
- Source SRID
- Serving SRID, defaulting to EPSG:3857 unless overridden
- Tile zoom limits
- Tile extent
- Tile buffer
- Geometry clipping behavior
- Cache folder/rules
- Cache TTL or freshness strategy
- Role/scope filtering rules
- Attribute whitelist for tile output
- Tile generation behavior

Example tile endpoint:

```http
GET /vector-tile-hub/tiles/97/13/9977/3110.pbf
```

### Security Scope

The specification must treat security scope as a first-class concept.

A scope may represent a role-based or data-partition-based view such as:

- admin
- engineer
- public
- district-specific or organization-specific access

The system should normally resolve scope from authenticated user claims and trusted server-side policy, not from arbitrary client input.

### Runtime Settings

The library must maintain runtime settings for cache behavior and active cache versions in a durable store.

These settings must be cached in memory for fast access.

## Required Architecture

The specification must preserve this logical separation:

```text
VectorTileHub API Endpoints
    ↓
Tile Orchestration Service
    ↓
Cache Service
    ↓
Security Scope Resolver
    ↓
Layer Config Provider
    ↓
Feature Provider
    ↓
MVT/PBF Encoder
```

The architecture must ensure:

- The encoder does not know about Oracle, SQL Server, SQLite, files, HTTP, or auth
- A provider does not know about HTTP endpoints or disk cache
- Cache services do not implement provider-specific query logic
- Security resolution is centralized and reusable
- Layer configuration loading is separate from request execution

## Recommended Project Structure

The generated specification should use a modular layout similar to:

```text
K1Soft.IT.VectorTileHub.Abstractions
K1Soft.IT.VectorTileHub.Core
K1Soft.IT.VectorTileHub.AspNetCore
K1Soft.IT.VectorTileHub.Providers.Oracle
K1Soft.IT.VectorTileHub.Providers.SqlServer
K1Soft.IT.VectorTileHub.Storage
K1Soft.IT.VectorTileHub.Jobs
```

Speckit may refine names, but modular boundaries must remain clear.

## Configuration Requirements

### Global App Settings

The library must support configuration from `appsettings.json`.

Representative shape:

```json
{
  "VectorTileHub": {
    "Enabled": true,
    "RoutePrefix": "/vector-tile-hub",
    "DefaultServingSrid": 3857,
    "DefaultTileExtent": 4096,
    "DefaultTileBuffer": 64,
    "LayerConfigFolder": "VectorTileHub/Layers",
    "DefaultCacheRootFolder": "VectorTileHub/Cache",
    "UseResponseCompression": true,
    "UseMemoryCache": true,
    "UseDiskCache": true,
    "UseRedisCache": false,
    "InternalSettingsStore": {
      "Provider": "Sqlite",
      "ConnectionString": "Data Source=VectorTileHub/vector_tile_hub.db"
    },
    "Hangfire": {
      "Enabled": true,
      "DashboardPath": "/vector-tile-hub/jobs",
      "RequiredRoles": [ "Admin", "GISAdmin" ]
    }
  }
}
```

If `InternalSettingsStore.ConnectionString` is absent, the library should default to a local SQLite settings database automatically.

### Per-Layer Files

Each layer must be configured in its own JSON file.

Representative folder layout:

```text
VectorTileHub/Layers/
    97-buildings.json
    98-parcels.json
    99-roads.json
```

Representative layer file shape:

```json
{
  "id": 97,
  "layerKey": "buildings",
  "layerName": "Jeddah Buildings",
  "enabled": true,
  "provider": {
    "type": "Oracle",
    "connectionString": "User Id=xxx;Password=xxx;Data Source=xxx",
    "tableName": "BUILDINGS",
    "idColumn": "ID",
    "geometryColumn": "GEOM_3857",
    "sourceSrid": 3857
  },
  "tile": {
    "minZoom": 12,
    "maxZoom": 21,
    "extent": 4096,
    "buffer": 64,
    "clipGeometry": true,
    "returnEmptyTileOutsideZoomRange": true
  },
  "attributes": {
    "include": [ "ID", "STATUS", "BUILDING_TYPE", "AREA_M2" ]
  }
}
```

The specification should define which settings are global, which are layer-specific, and which can be overridden per layer.

## Internal Settings Store

The server must store runtime cache settings in a database table or equivalent durable store.

It must include enough information to track:

- Active cache folder or version per layer
- Pending cache builds
- Cache generation status
- Cache invalidation state
- Last update timestamps
- Operational metadata needed by admin workflows

The store may use EF Core for metadata and runtime tables, but the hot tile query path must not depend on EF Core tracking.

## Tile Request Behavior

The specification must define the request pipeline for:

1. Validating layer existence and enablement
2. Validating z/x/y bounds and zoom rules
3. Resolving security scope from the authenticated principal
4. Building the cache key
5. Checking memory cache if enabled
6. Checking disk cache if enabled
7. Generating tiles on miss if allowed
8. Returning a valid empty tile where required
9. Writing cache entries using the current active cache strategy

## Empty Tile Behavior

The server must return a valid empty MVT/PBF tile when:

- A layer exists but has no visible features in the requested tile
- The user is authorized but no records match the resolved scope
- The tile is outside configured data coverage but still within a valid request shape
- Layer policy explicitly requires empty output rather than errors for certain conditions

The specification must distinguish:

- Empty tile
- Not found layer
- Unauthorized request
- Invalid tile coordinate request

## Cache Design

The spec must define:

- Disk cache path structure
- Cache key structure
- Inclusion of scope in cache identity
- Active cache version/folder handling
- Freshness rules
- Cache invalidation rules
- Background generation behavior
- Safe replacement behavior

The resolved `scopeKey` must be part of the cache key and disk cache path.

The design should prefer role/scope cache segmentation, not per-user cache segmentation by default.

## Security Requirements

Security must be enforced server-side.

The specification must require:

- No unauthorized records included in output tiles
- No reliance on frontend filtering
- Authorization on admin endpoints
- Trusted server-side scope resolution
- Parameterized SQL only
- No direct concatenation of user-controlled SQL fragments

Optional `?scope=` input may exist for advanced scenarios, but the normal trusted path should derive scope from authenticated roles/claims and server policy.

## Data Provider Architecture

The library must support multiple providers through a shared abstraction.

Every provider must return features in a normalized form suitable for encoding.

Normalized features should include:

- Feature identifier
- Geometry as `NetTopologySuite.Geometries.Geometry`
- Whitelisted attributes
- Optional metadata needed for downstream tile encoding

Geometry should preferably already be transformed to EPSG:3857 before encoding.

### Provider Requirements

At minimum, the specification must cover:

- Oracle provider
- SQL Server provider

The provider design must remain open for future providers such as PostGIS, SQLite/SpatiaLite, file-based sources, or custom services.

### Oracle Provider

The Oracle provider must:

- Use raw SQL / ADO.NET or similarly low-overhead access for hot-path spatial queries
- Support geometry retrieval and bounding-box filtering
- Support server-side attribute projection
- Support secure parameterization
- Avoid unsafe SQL concatenation

### SQL Server Provider

The SQL Server provider must:

- Use raw SQL / ADO.NET or similarly low-overhead access for hot-path spatial queries
- Support geometry retrieval and bounding-box filtering
- Support server-side attribute projection
- Support secure parameterization
- Avoid unsafe SQL concatenation

## MVT/PBF Encoding Requirements

The encoder must generate valid Mapbox Vector Tile-compatible PBF bytes.

The specification must define:

- Default MVT extent of 4096
- Default tile buffer of 64 unless overridden
- Geometry clipping behavior
- Attribute whitelisting behavior
- Empty tile encoding behavior
- Layer naming within the tile

The encoder must remain independent from database/provider concerns.

## Background Jobs

Use Hangfire for background jobs.

The specification must define jobs for:

- Cache generation
- Cache deletion
- Cache invalidation
- Cache build/swap workflows
- Processing change notifications

The host application must be able to customize Hangfire dashboard authorization.

Representative dashboard path:

```http
/vector-tile-hub/jobs
```

## Cache Generation and Replacement

The specification must describe a safe two-stage cache replacement strategy:

1. Create a new cache folder/version
2. Atomically switch active runtime settings to the new cache folder immediately
3. Serve requests using the new active cache folder
4. Generate or refill cache content into the new active cache folder in the background
5. Delete the old cache as a later background operation

The purpose of this design is to avoid the performance cost of deleting a very large cache with many small files before switching.

The system may switch traffic to a new empty cache folder first, then let the server rebuild and serve through that new cache.

## Cache Invalidation from Data Changes

The specification should support external notifications when source records change.

It must describe how to invalidate or regenerate affected tiles when:

- Features are inserted
- Features are updated
- Features are deleted
- Security scope visibility changes

For updates and deletes, old and new bounding boxes may both need consideration.

For scope changes, both old and new scopes may need invalidation.

## Tile Math

The server must use XYZ addressing.

The specification should define reliable utilities for:

- Tile bounds from z/x/y
- Bounding-box expansion by buffer
- Coordinate conversions needed by serving logic
- Coverage calculations used in cache generation/invalidation

## Cost Control

The specification must protect the server from expensive requests.

Layer rules should support:

- Minimum zoom thresholds
- Maximum zoom thresholds
- Optional generation restrictions at certain zooms
- Control over on-demand generation
- Sensible fallback behavior on expensive misses

## SQL Safety

All SQL must be parameterized.

Layer configuration may define SQL templates, but user-controlled input must never be directly concatenated into executable SQL.

Security filters must be produced from trusted server-side rules and resolved scopes.

## API Endpoints

### Public Tile Endpoint

```http
GET /vector-tile-hub/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf
```

Optional query example:

```http
?scope=engineer
```

### Layer Metadata Endpoints

```http
GET /vector-tile-hub/layers
GET /vector-tile-hub/layers/{layerId:int}
```

Returned metadata must be frontend-safe and may include:

- Layer ID
- Layer name
- Min zoom
- Max zoom
- Available scopes, if exposing them is allowed
- Tile URL template

### Admin Cache Endpoints

```http
POST /vector-tile-hub/admin/layers/{layerId:int}/cache/generate
POST /vector-tile-hub/admin/layers/{layerId:int}/cache/delete
POST /vector-tile-hub/admin/layers/{layerId:int}/cache/invalidate
POST /vector-tile-hub/admin/layers/{layerId:int}/cache/notify-change
POST /vector-tile-hub/admin/layers/{layerId:int}/cache/swap
GET  /vector-tile-hub/admin/layers/{layerId:int}/cache/status
```

Admin endpoints must require authorization.

## Frontend Expectations

The typical consumer is an OpenLayers-based frontend, but the library must remain frontend-agnostic.

The specification should optimize for:

- Lightweight tile payloads
- Stable URL contracts
- Safe metadata exposure
- Efficient rendering attributes only

PBF output should contain only attributes required for rendering and lightweight identification.

## Sensitive Data Policy

The generated specification must state that:

- Unauthorized data must never be encoded into tiles
- Sensitive columns must not be emitted unless explicitly allowed
- Attribute whitelisting is mandatory, not optional by default
- Connection strings and internal provider secrets must never be exposed via metadata endpoints

## Non-Goals for First Version

The first version should not require:

- A full GIS desktop administration UI
- Complex visual style management
- Per-user cache by default
- Provider-specific logic leaking into the shared encoder
- Over-engineered distributed architecture for day one

## Preferred Implementation Notes

These are preferred defaults unless the spec needs a stronger alternative:

- Use ASP.NET Core
- Use C#
- Use NetTopologySuite for geometry
- Use raw SQL / ADO.NET for hot-path spatial queries
- Use EF Core only where appropriate for internal settings/metadata
- Use SQLite as the default internal settings database when nothing else is configured
- Use Hangfire for background jobs
- Use parameterized SQL only
- Keep provider implementations replaceable
- Use EPSG:3857 as the default serving projection
- Use MVT extent 4096
- Use tile buffer 64 by default

## Suggested Core Interfaces

The final generated specification should include or refine interfaces along these lines:

```csharp
public interface IVectorTileService
{
    Task<VectorTileResult> GetTileAsync(
        int layerId,
        int z,
        int x,
        int y,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IVectorTileFeatureProvider
{
    Task<VectorTileFeatureBatch> GetFeaturesAsync(
        VectorTileFeatureQuery query,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IVectorTileEncoder
{
    byte[] Encode(
        string mvtLayerName,
        IReadOnlyList<VectorTileFeature> features,
        VectorTileEncodingContext context);
}
```

```csharp
public interface IVectorTileCache
{
    Task<byte[]?> GetAsync(
        VectorTileCacheKey key,
        CancellationToken cancellationToken);

    Task SetAsync(
        VectorTileCacheKey key,
        byte[] tileBytes,
        VectorTileCacheOptions options,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        VectorTileCacheKey key,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IVectorTileSecurityScopeResolver
{
    Task<VectorTileSecurityScope> ResolveAsync(
        VectorTileLayerConfig layer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IVectorTileRuntimeSettingsStore
{
    Task<VectorTileLayerRuntimeSettings> GetLayerRuntimeSettingsAsync(
        int layerId,
        CancellationToken cancellationToken);

    Task UpdateLayerRuntimeSettingsAsync(
        VectorTileLayerRuntimeSettings settings,
        CancellationToken cancellationToken);
}
```

## Acceptance Criteria

The resulting specification must make it possible to implement a first release where:

- The library plugs into an ASP.NET Core host cleanly
- A sample ASP.NET Core application demonstrates real usage of the library
- Layers are loaded from configuration files
- Oracle and SQL Server providers are both supported
- Tiles can be served dynamically
- Empty tiles are valid and intentional
- Cache is scope-aware
- Cache generation and deletion run in background jobs
- Cache replacement is safe and non-destructive
- Runtime cache settings persist in a durable store
- Unauthorized data is never returned in tile output

## Final Instruction to Speckit

Produce a concrete technical specification for implementation.

Keep it opinionated where necessary, but do not overdesign.

Make important tradeoffs explicit, especially around:

- cache identity
- scope resolution
- provider boundaries
- SQL safety
- runtime settings
- tile generation on miss
- background replacement strategy

If you introduce assumptions, list them clearly in a dedicated assumptions section rather than hiding them in prose.
