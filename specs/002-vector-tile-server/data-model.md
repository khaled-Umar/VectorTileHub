# Data Model: VectorTileHub (002 — host-agnostic, variant-based)

**Date**: 2026-06-01
**Branch**: `002-vector-tile-server`

Differences from 001 are flagged **[CHANGED]**, **[NEW]**, or **[REMOVED]**.

## Configuration Models (from JSON / appsettings)

### VectorTileHubOptions

Global library settings under the `VectorTileHub` section of `appsettings.json`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Master enable/disable |
| RoutePrefix | string | "/vector-tile-hub" | Base path for endpoints (host still owns exposure/security) |
| DefaultServingSrid | int | 3857 | Output SRID (EPSG:3857) |
| DefaultTileExtent | int | 4096 | MVT extent |
| DefaultTileBuffer | int | 64 | Tile buffer |
| LayerConfigPaths | string[] | [] | **[CHANGED]** Explicit file paths (any location) to per-layer config files; host points at them |
| LayerConfigFolder | string | "VectorTileHub/Layers" | Optional folder scanned for layer files (in addition to paths) |
| DefaultCacheRootFolder | string | "VectorTileHub/Cache" | Root for disk cache |
| UseResponseCompression | bool | true | Enable response compression |
| UseMemoryCache | bool | true | Enable in-memory tile cache layer |
| UseDiskCache | bool | true | Enable disk cache layer |
| InternalSettingsStore | SettingsStoreOptions | (below) | Runtime settings store config |
| Hangfire | HangfireOptions | (below) | Background job config |
| HealthCheckPath | string | "/vector-tile-hub/health" | Health indicator path |

**[REMOVED]** `DefaultAuthenticationRequired` — security is host-owned.

### SettingsStoreOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Provider | string | "Sqlite" | "Sqlite" (auto-created) or a host-supplied provider |
| ConnectionString | string? | "Data Source=VectorTileHub/vector_tile_hub.db" | Host-supplied connection; if null/absent → auto-create SQLite |

### HangfireOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Enable background jobs |
| DashboardPath | string | "/vector-tile-hub/jobs" | Dashboard mount path |

**[REMOVED]** `RequiredRoles` — dashboard authorization is supplied by the
host at mount time (an `IDashboardAuthorizationFilter` / delegate), not by
library config.

### VectorTileLayerConfig

One file per layer (locatable anywhere; registered by path).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | int | yes | **Unique** integer layer id (duplicates rejected at load) |
| LayerKey | string | yes | Unique string key (used as MVT layer name) |
| LayerName | string | yes | Human-readable display name |
| Enabled | bool | yes | Whether the layer is active |
| Provider | ProviderConfig | yes | Data source configuration (own connection string) |
| Tile | TileConfig | no | Tile settings (defaults from global) |
| Attributes | AttributeConfig | yes | Attribute whitelist |
| CacheRules | CacheRuleConfig[] | no | **[CHANGED]** Filtered cache variants (replaces `Security`) |
| Cache | LayerCacheConfig | no | Per-layer cache overrides (incl. refresh period) |

**[REMOVED]** `Security` (`SecurityConfig`) — no auth/scope in the library.

### ProviderConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Type | string | yes | "SqlServer" or "Oracle" |
| ConnectionStringName | string? | no* | Named connection from host config |
| ConnectionString | string? | no* | Direct connection string (**per-layer**); one of Name/String required |
| TableName | string | yes | Source table/view (fully qualified) |
| IdColumn | string | yes | Feature id column |
| GeometryColumn | string | yes | Geometry column |
| SourceSrid | int | yes | SRID of source geometry |

\* Per spec, each layer may have its own connection string; either a named
reference or a direct string is required.

### TileConfig

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| MinZoom | int | 0 | Minimum served zoom |
| MaxZoom | int | 21 | Maximum served zoom |
| Extent | int | (global) | MVT extent override |
| Buffer | int | (global) | Buffer override |
| ClipGeometry | bool | true | Clip geometry to tile bounds |
| ReturnEmptyTileOutsideZoomRange | bool | true | Empty tile (not error) outside range |
| AllowOnDemandGeneration | bool | true | Generate on cache miss |

### AttributeConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Include | string[] | yes | Whitelist of columns emitted into MVT |

### CacheRuleConfig **[NEW]**

Defines one filtered cache variant for a layer.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| VariantKey | string | yes | Key callers pass to select this variant; unique within the layer |
| IsDefault | bool | no | Marks the variant served when no key is supplied (exactly one default; if none declared, an unfiltered `default` variant is implied) |
| Filter | FilterConfig | no | Server-side, parameterized predicate that scopes the source rows for this variant |
| DisplayName | string? | no | Human-readable label |

### FilterConfig **[NEW]**

A trusted, server-side filter definition (never raw client SQL).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Column | string | yes | Source column to filter on |
| Operator | enum | yes | `Equals`, `In`, `NotEquals`, `IsNull`, `IsNotNull` |
| Values | string[] | no | Parameterized values bound at query time |

### LayerCacheConfig

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Enable caching for this layer |
| CacheRootFolder | string | (global) | Override cache root |
| RefreshPeriodMinutes | int | 0 | **[CHANGED]** Tile age after which a tile is stale (0 = never stale); drives stale-while-revalidate |

## Runtime Entities (persisted in settings store)

### LayerVariantRuntimeSettings **[CHANGED]**

Durable per-(layer, variant) operational state. One row per layer+variant.

| Field | Type | Description |
|-------|------|-------------|
| LayerId | int (PK part) | Layer id (matches config) |
| VariantKey | string (PK part) | Variant key (`default` for the default) |
| ActiveCacheVersion | string | Current active cache version folder |
| CacheGenerationStatus | enum | Idle, Running, Failed |
| CacheGenerationJobId | string? | Hangfire job id for running generation |
| LastGenerationStartedAt | DateTimeOffset? | Last generation start |
| LastGenerationCompletedAt | DateTimeOffset? | Last generation completion |
| LastInvalidatedAt | DateTimeOffset? | Last invalidation |
| UpdatedAt | DateTimeOffset | Last modification |
| Metadata | string? | Extensible JSON |

**State transitions**:
```
Idle → Running (generation/refresh triggered)
Running → Idle (completed successfully)
Running → Failed (error — partial cache retained, tiles already written stay usable)
Failed → Running (manual retry)
```

### ServerSetting **[NEW, global key/value]**

| Field | Type | Description |
|-------|------|-------------|
| Key | string (PK) | e.g. `ActiveCacheRootPath` |
| Value | string | Setting value |
| UpdatedAt | DateTimeOffset | Last modification |

Mirrored in memory; reads served from memory, writes write-through + refresh
the mirror (FR-026–FR-029).

## Request-Scoped Models (in-memory only)

### VectorTileFeatureQuery **[CHANGED]**

| Field | Type | Description |
|-------|------|-------------|
| LayerConfig | VectorTileLayerConfig | Full layer config |
| Envelope | Envelope | Bounding box in serving SRID (with buffer) |
| Zoom | int | Zoom level |
| Variant | VectorTileVariant | **Resolved variant** (key + parameterized filter) — replaces `SecurityScope` |

### VectorTileVariant **[NEW]** (replaces VectorTileSecurityScope)

| Field | Type | Description |
|-------|------|-------------|
| VariantKey | string | Canonical variant key (part of cache key) |
| Filter | ResolvedFilter? | Parameterized column/operator/values applied server-side; null = unfiltered |
| IsDefault | bool | Whether this is the layer's default variant |

### VectorTileFeature / VectorTileFeatureBatch (carried from 001)

- **VectorTileFeature**: `Id` (object), `Geometry` (NTS, serving SRID),
  `Attributes` (whitelisted key/value map).
- **VectorTileFeatureBatch**: `Features` (list), `TotalCount`.

### VectorTileCacheKey **[CHANGED]**

| Field | Type | Description |
|-------|------|-------------|
| LayerId | int | Layer id |
| Z / X / Y | int | Tile coordinates |
| VariantKey | string | Resolved variant key (**replaces** ScopeKey) |
| CacheVersion | string | Active cache version |

**String**: `{LayerId}:{VariantKey}:{CacheVersion}:{Z}:{X}:{Y}`
**Disk path**: `{CacheRoot}/{LayerId}/{VariantKey}/{CacheVersion}/{Z}/{X}/{Y}.pbf`

### VectorTileEncodingContext (carried)

`LayerKey`, `Extent`, `Buffer`, `ClipGeometry`, `TileEnvelope`.

### VectorTileResult **[CHANGED]**

| Field | Type | Description |
|-------|------|-------------|
| TileBytes | byte[] | MVT/PBF content (empty array for empty tile) |
| IsEmpty | bool | Whether empty |
| FromCache | bool | Served from cache |
| IsStale | bool | **[NEW]** Served stale; a background refresh was enqueued |
| ContentType | string | "application/x-protobuf" |

## Sample-only Models

### SldRule → GL style layer (see `contracts/sld-style.md`)

Parsed from `tmp/layerStyle.sld`: `{ Name, Type_t value(s), FillColor,
StrokeColor, StrokeWidth, StrokeLinejoin }` plus the label rule
`{ labelProperties, maxScaleDenominator }`. Not part of the library.

## Entity Relationships

```text
VectorTileHubOptions (1)
    └── has many → VectorTileLayerConfig (N)
                      ├── has one  → ProviderConfig
                      ├── has one  → TileConfig
                      ├── has one  → AttributeConfig
                      ├── has many → CacheRuleConfig (variants; one IsDefault)
                      │                 └── has one → FilterConfig
                      ├── has one  → LayerCacheConfig
                      └── has many → LayerVariantRuntimeSettings (per layer+variant)

VectorTileFeatureQuery → uses VectorTileLayerConfig + VectorTileVariant
                       → produces VectorTileFeatureBatch → VectorTileFeature (N)

VectorTileCacheKey ← (LayerId, z, x, y, VariantKey, CacheVersion)
VectorTileResult  ← produced by VectorTileOrchestrator (may be stale + refresh enqueued)
ServerSetting (global key/value, memory-mirrored)
```

## Validation Rules (from spec requirements)

- Layer `Id` MUST be unique across all loaded layer files (FR-008).
- Each layer MUST declare a connection (name or string), table, id column,
  geometry column, source SRID (FR-006/FR-007); missing/invalid → that layer
  is not served, error names file + field.
- At most one `CacheRuleConfig.IsDefault = true` per layer; unknown requested
  variant key → "variant not found" (FR-024).
- Requests outside `[MinZoom, MaxZoom]` → empty tile (FR-013).
- `Attributes.Include` is the only source of emitted attributes (FR-012).
- Filters are parameterized only; no raw client SQL (constitution SQL standard).
