# Data Model: VectorTileHub

**Date**: 2026-05-24
**Branch**: `001-vector-tile-engine`

## Configuration Models (read from JSON files)

### VectorTileHubOptions

Global library settings loaded from `appsettings.json` under the
`VectorTileHub` section.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Master enable/disable for the library |
| RoutePrefix | string | "/vector-tile-hub" | Base path for all endpoints |
| DefaultServingSrid | int | 3857 | Default output SRID (EPSG:3857) |
| DefaultTileExtent | int | 4096 | Default MVT extent |
| DefaultTileBuffer | int | 64 | Default tile buffer in pixels |
| LayerConfigFolder | string | "VectorTileHub/Layers" | Path to layer JSON files |
| DefaultCacheRootFolder | string | "VectorTileHub/Cache" | Root path for disk cache |
| UseResponseCompression | bool | true | Enable response compression |
| UseMemoryCache | bool | true | Enable in-memory cache layer |
| UseDiskCache | bool | true | Enable disk cache layer |
| DefaultAuthenticationRequired | bool | true | Require auth for layers without explicit security rules |
| InternalSettingsStore | SettingsStoreOptions | (see below) | Runtime settings database config |
| Hangfire | HangfireOptions | (see below) | Background job config |
| HealthCheckPath | string | "/vector-tile-hub/health" | Health check endpoint path |

### SettingsStoreOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Provider | string | "Sqlite" | Storage provider type |
| ConnectionString | string | "Data Source=VectorTileHub/vector_tile_hub.db" | Connection string |

### HangfireOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Enable background jobs |
| DashboardPath | string | "/vector-tile-hub/jobs" | Dashboard URL path |
| RequiredRoles | string[] | ["Admin"] | Roles allowed to access dashboard |

### VectorTileLayerConfig

Per-layer configuration loaded from individual JSON files in
`LayerConfigFolder`. One file per layer.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | int | yes | Unique layer identifier |
| LayerKey | string | yes | Unique string key (used in MVT layer name) |
| LayerName | string | yes | Human-readable display name |
| Enabled | bool | yes | Whether layer is active |
| Provider | ProviderConfig | yes | Data source configuration |
| Tile | TileConfig | no | Tile rendering settings (defaults from global) |
| Attributes | AttributeConfig | yes | Attribute whitelist |
| Security | SecurityConfig | no | Scope filtering rules (null = use global default) |
| Cache | LayerCacheConfig | no | Per-layer cache overrides |

### ProviderConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Type | string | yes | Provider type key ("SqlServer", "Oracle") |
| ConnectionStringName | string | no | Named connection string from host config |
| ConnectionString | string | no | Direct connection string (one of Name or String required) |
| TableName | string | yes | Source table or view (fully qualified) |
| IdColumn | string | yes | Primary key / feature ID column |
| GeometryColumn | string | yes | Spatial geometry column name |
| SourceSrid | int | yes | SRID of the source geometry |
| CustomFilter | string | no | Additional WHERE clause (server-side only, parameterized) |

### TileConfig

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| MinZoom | int | 0 | Minimum allowed zoom level |
| MaxZoom | int | 21 | Maximum allowed zoom level |
| Extent | int | (global) | MVT extent override |
| Buffer | int | (global) | Tile buffer override |
| ClipGeometry | bool | true | Clip geometry to tile bounds |
| ReturnEmptyTileOutsideZoomRange | bool | true | Empty tile vs 400 outside range |
| AllowOnDemandGeneration | bool | true | Generate on cache miss |

### AttributeConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Include | string[] | yes | Whitelist of column names to emit in MVT output |

### SecurityConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| RequireAuthentication | bool | no | Override global auth default for this layer |
| ScopeColumn | string | no | Column used for scope-based filtering |
| ScopeMappings | Dictionary&lt;string, string[]&gt; | no | Role → allowed scope values mapping |

### LayerCacheConfig

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Enable caching for this layer |
| CacheRootFolder | string | (global) | Override cache root |
| TtlMinutes | int | 0 | Cache TTL (0 = indefinite until invalidated) |

## Runtime Entities (persisted in settings store)

### LayerRuntimeSettings

Durable per-layer operational state stored in the internal settings
database. One row per layer.

| Field | Type | Description |
|-------|------|-------------|
| LayerId | int (PK) | Layer identifier (matches config) |
| ActiveCacheVersion | string | Current active cache folder/version name |
| CacheGenerationStatus | enum | Idle, Running, Failed |
| CacheGenerationJobId | string? | Hangfire job ID for running generation |
| LastGenerationStartedAt | DateTimeOffset? | When last generation began |
| LastGenerationCompletedAt | DateTimeOffset? | When last generation finished |
| LastInvalidatedAt | DateTimeOffset? | When last invalidation occurred |
| UpdatedAt | DateTimeOffset | Last modification timestamp |
| Metadata | string? | Extensible JSON blob |

**State transitions**:
```
Idle → Running (generation triggered)
Running → Idle (generation completed successfully)
Running → Failed (generation error — partial cache retained)
Failed → Running (manual retry triggered)
```

## Request-Scoped Models (in-memory only)

### VectorTileFeatureQuery

Parameters passed from the orchestrator to a provider to fetch
features for a specific tile.

| Field | Type | Description |
|-------|------|-------------|
| LayerConfig | VectorTileLayerConfig | Full layer configuration |
| Envelope | Envelope | Bounding box in serving SRID (with buffer) |
| Zoom | int | Current zoom level |
| SecurityScope | VectorTileSecurityScope | Resolved scope for filtering |

### VectorTileFeature

A single geospatial feature returned by a provider, ready for
encoding.

| Field | Type | Description |
|-------|------|-------------|
| Id | object | Feature identifier (typically long or string) |
| Geometry | Geometry (NTS) | Feature geometry in serving SRID |
| Attributes | IReadOnlyDictionary&lt;string, object&gt; | Whitelisted attribute key-value pairs |

### VectorTileFeatureBatch

| Field | Type | Description |
|-------|------|-------------|
| Features | IReadOnlyList&lt;VectorTileFeature&gt; | Features for the requested tile |
| TotalCount | int | Count of features returned |

### VectorTileSecurityScope

| Field | Type | Description |
|-------|------|-------------|
| ScopeKey | string | Canonical scope identifier (used in cache key) |
| IsAuthenticated | bool | Whether the request is authenticated |
| FilterValues | string[]? | Scope values to filter against in the provider query |
| Principal | ClaimsPrincipal | Original claims principal |

### VectorTileCacheKey

Composite key for cache lookup and storage.

| Field | Type | Description |
|-------|------|-------------|
| LayerId | int | Layer identifier |
| Z | int | Zoom level |
| X | int | Tile column |
| Y | int | Tile row |
| ScopeKey | string | Resolved scope key |
| CacheVersion | string | Active cache version |

**String representation**: `{LayerId}:{ScopeKey}:{CacheVersion}:{Z}:{X}:{Y}`

**Disk path**: `{CacheRoot}/{LayerId}/{ScopeKey}/{CacheVersion}/{Z}/{X}/{Y}.pbf`

### VectorTileEncodingContext

| Field | Type | Description |
|-------|------|-------------|
| LayerKey | string | MVT layer name in the tile |
| Extent | int | MVT extent (default 4096) |
| Buffer | int | Tile buffer (default 64) |
| ClipGeometry | bool | Whether to clip geometry to tile bounds |
| TileEnvelope | Envelope | Tile bounding box in serving SRID |

### VectorTileResult

| Field | Type | Description |
|-------|------|-------------|
| TileBytes | byte[] | MVT/PBF binary content |
| IsEmpty | bool | Whether this is an empty tile |
| FromCache | bool | Whether served from cache |
| ContentType | string | "application/x-protobuf" |

### VectorTileCacheOptions

| Field | Type | Description |
|-------|------|-------------|
| TtlMinutes | int | Time-to-live (0 = indefinite) |
| CacheVersion | string | Version folder to write into |

## Entity Relationships

```text
VectorTileHubOptions (1)
    └── has many → VectorTileLayerConfig (N)
                      ├── has one → ProviderConfig
                      ├── has one → TileConfig
                      ├── has one → AttributeConfig
                      ├── has one → SecurityConfig (optional)
                      ├── has one → LayerCacheConfig (optional)
                      └── has one → LayerRuntimeSettings (1:1, by LayerId)

VectorTileFeatureQuery → uses → VectorTileLayerConfig
                       → uses → VectorTileSecurityScope
                       → produces → VectorTileFeatureBatch
                                      └── contains → VectorTileFeature (N)

VectorTileCacheKey → derived from → (LayerId, z, x, y, ScopeKey, CacheVersion)

VectorTileResult → produced by → VectorTileOrchestrator
```
