# Feature Specification: VectorTileHub — Reusable Vector Tile Engine

**Feature Branch**: `001-vector-tile-engine`

**Created**: 2026-05-24

**Status**: Draft

**Input**: Seed file `VectorTileHub.spec-seed.md` — reusable ASP.NET Core library for secure, cache-aware, provider-agnostic vector tile serving

## Clarifications

### Session 2026-05-24

- Q: When a layer has no security scope filtering rules, should it require authentication or be public? → A: Configurable global default — host chooses the default posture; recommended default is "require auth".
- Q: What level of observability should the library provide? → A: Structured logging (requests, errors, cache hits/misses) + health check endpoint for load balancers and monitoring.
- Q: When a background cache generation job fails mid-execution, what should happen? → A: Mark failed, keep partial cache (tiles already written remain usable), allow manual retry.
- Q: Should layer configuration changes take effect without restarting the application? → A: Yes, but only via explicit admin endpoint trigger (no file watching).
- Q: When the notify-change endpoint is called, how should affected tiles be identified? → A: Caller provides a bounding box, system computes affected tile coordinates across zoom levels.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Serve Vector Tiles from a Data Source (Priority: P1)

A host application developer adds VectorTileHub to their ASP.NET Core
application. They configure a data source and layer, then expose a
tile endpoint. A map frontend requests tiles by layer, zoom, and
coordinates and receives valid MVT/PBF binary responses containing
only whitelisted attributes for the requested area.

**Why this priority**: Without dynamic tile serving, no other feature
has value. This is the core capability of the library.

**Independent Test**: Configure one layer pointing at a single
database table, request a tile at a known coordinate containing data,
and verify the response is a valid MVT/PBF binary with the expected
attributes.

**Acceptance Scenarios**:

1. **Given** a host app with VectorTileHub configured and one layer
   enabled, **When** a client requests
   `GET /vector-tile-hub/tiles/{layerId}/{z}/{x}/{y}.pbf` for a tile
   containing features, **Then** the server returns HTTP 200 with
   content-type `application/x-protobuf` and the response is a valid
   MVT tile containing only whitelisted attributes.

2. **Given** a configured layer with zoom range 12–21, **When** a
   client requests a tile at zoom level 10, **Then** the server
   returns a valid empty MVT tile (not an error).

3. **Given** a configured layer, **When** a client requests a tile
   coordinate that contains no features, **Then** the server returns
   a valid empty MVT tile with HTTP 200.

4. **Given** no layer exists for the requested layer ID, **When** a
   client requests a tile, **Then** the server returns HTTP 404.

5. **Given** a layer with attribute whitelist
   `["Id", "DISTRICT", "PARCELNUMBER"]`, **When** a tile is
   generated, **Then** only those three attributes appear in the
   MVT output — no audit fields, no concurrency stamps, no internal
   identifiers.

---

### User Story 2 — Secure Scope-Aware Tile Serving (Priority: P2)

An authenticated user requests tiles. The system resolves a security
scope from the user's identity and trusted server-side policy.
Tiles are generated containing only records the user is authorized
to see. Unauthorized records are never encoded into tile output,
regardless of what the frontend requests.

**Why this priority**: Security is non-negotiable per the project
constitution. Tiles without access control would leak sensitive
geospatial data.

**Independent Test**: Configure a layer with scope-based filtering
rules. Request tiles as two different users with different scopes
and verify each user receives only their authorized features.

**Acceptance Scenarios**:

1. **Given** a layer with scope-based access rules and an
   authenticated user with scope "engineer", **When** the user
   requests a tile, **Then** only features visible to scope
   "engineer" are encoded in the tile.

2. **Given** an unauthenticated request to a layer requiring
   authorization, **When** the tile is requested, **Then** the
   server returns HTTP 401 (not an empty tile with all data
   stripped).

3. **Given** two users with different scopes requesting the same
   tile coordinate, **When** tiles are generated, **Then** each
   user receives a different tile containing only their authorized
   features — the system never serves a scope-incorrect cached tile.

4. **Given** an admin user requesting a tile, **When** the security
   scope resolver runs, **Then** the scope is derived from server-
   side policy and user claims, not from arbitrary client input.

---

### User Story 3 — Cache-Aware Tile Serving (Priority: P3)

The system caches generated tiles to avoid redundant database work.
When a tile has already been generated for a given layer, coordinate,
and scope, repeat requests are served from cache. Cache keys include
scope identity so different users with different scopes receive
correctly scoped cached tiles.

**Why this priority**: Without caching, every tile request hits the
database. For high-volume map usage, caching is essential for
acceptable performance and cost control.

**Independent Test**: Request a tile twice for the same layer,
coordinate, and scope. Verify the second request is served from
cache (observably faster, no database query executed).

**Acceptance Scenarios**:

1. **Given** disk cache is enabled and a tile has not been cached,
   **When** the tile is requested and generated, **Then** the tile
   bytes are written to the disk cache using a key that includes
   layer ID, z/x/y coordinates, and resolved scope.

2. **Given** a tile exists in disk cache for the requested key,
   **When** the same tile is requested again, **Then** the cached
   tile is returned without querying the database.

3. **Given** memory cache is enabled on top of disk cache, **When**
   a tile is requested, **Then** the system checks memory cache
   first, then disk cache, then generates on miss.

4. **Given** two users with different security scopes, **When** each
   requests the same tile coordinate, **Then** separate cache entries
   exist for each scope — one user's cached tile is never served to
   the other.

---

### User Story 4 — Background Cache Management (Priority: P4)

An operator triggers cache operations through admin endpoints:
generating cache for a layer, invalidating specific tiles after data
changes, deleting old cache, or performing a safe cache replacement.
These operations run as background jobs without blocking tile serving.

**Why this priority**: Proactive cache generation and safe
replacement are critical for operational efficiency, but the system
can function (with on-demand generation) without them.

**Independent Test**: Trigger a cache generation job for one layer
via admin endpoint, verify it runs in the background, and confirm
tiles are written to the cache folder.

**Acceptance Scenarios**:

1. **Given** an operator with admin authorization, **When** they POST
   to the cache generation endpoint for a layer, **Then** a
   background job is enqueued and the endpoint returns immediately
   with a job reference.

2. **Given** a running cache generation job, **When** tile requests
   arrive for the same layer, **Then** requests are served normally
   (from existing cache or on-demand generation) without waiting for
   the background job.

3. **Given** a cache replacement is triggered, **When** the system
   performs the swap, **Then** it: (a) creates a new cache
   folder/version, (b) immediately switches the active runtime
   setting to the new folder, (c) begins serving through the new
   (initially empty) cache, (d) generates cache content in the
   background, (e) deletes the old cache as a later background task.

4. **Given** source data has changed for specific features, **When**
   an operator calls the notify-change endpoint, **Then** affected
   tiles are invalidated and will be regenerated on next request or
   via background job.

5. **Given** admin cache endpoints exist, **When** an unauthenticated
   or unauthorized user attempts to call them, **Then** the server
   returns HTTP 401 or 403.

---

### User Story 5 — Multi-Provider Layer Configuration (Priority: P5)

A host developer configures layers from multiple data providers. One
layer may read from SQL Server, another from Oracle, a future one
from PostGIS. Each layer specifies its provider type and connection
in its own configuration file. The core tile serving, caching, and
security logic works identically regardless of provider.

**Why this priority**: Provider independence is an architectural
principle, but the system delivers full value with a single provider.
Multi-provider support extends reach without changing core behavior.

**Independent Test**: Configure two layers with different provider
types (e.g., SQL Server and Oracle). Request tiles from each and
verify both return valid MVT tiles through the same endpoint pattern.

**Acceptance Scenarios**:

1. **Given** a layer configured with provider type "SqlServer" and
   another with type "Oracle", **When** tiles are requested from
   each layer, **Then** both produce valid MVT/PBF output through the
   same tile endpoint pattern.

2. **Given** a new provider is implemented, **When** the operator
   creates a layer config file referencing the new provider type,
   **Then** tiles can be served from that provider without changes to
   the core library.

3. **Given** a layer configuration file, **When** it is loaded,
   **Then** provider-specific settings (connection string, table
   name, geometry column) are isolated within the provider section
   and do not affect tile serving, caching, or security logic.

---

### User Story 6 — Layer Metadata and Discovery (Priority: P6)

A frontend or API consumer requests a list of available layers and
their metadata (name, zoom range, tile URL template). The metadata
endpoints return only frontend-safe information and never expose
connection strings, internal credentials, or security policy details.

**Why this priority**: Layer discovery enables dynamic map
configuration, but tiles can be served without it if layer IDs are
known ahead of time.

**Independent Test**: Request the layers list endpoint, verify it
returns layer metadata without any internal or sensitive information.

**Acceptance Scenarios**:

1. **Given** three layers are configured (two enabled, one disabled),
   **When** a client requests the layers endpoint, **Then** only the
   two enabled layers appear in the response with safe metadata
   (ID, name, zoom range, tile URL template).

2. **Given** a layer has a connection string and security rules in
   its config, **When** metadata is requested, **Then** the response
   contains no connection strings, no provider secrets, and no
   internal security policy details.

---

### User Story 7 — Sample Application Integration (Priority: P7)

A sample ASP.NET Core application demonstrates realistic integration
with the VectorTileHub library. It configures a SQL Server layer
against a known test database, serves tiles, and verifies the
library works in a real host environment.

**Why this priority**: The sample is not the product — it exists to
validate that the library integrates cleanly. It is developed after
core library functionality is complete.

**Independent Test**: Run the sample application, navigate to a tile
URL for the configured layer, and verify a valid MVT tile is
returned containing only the whitelisted attributes.

**Acceptance Scenarios**:

1. **Given** the sample application references the VectorTileHub
   library, **When** the application starts, **Then** it configures
   VectorTileHub, registers a SQL Server provider, and maps tile
   endpoints without errors.

2. **Given** the sample layer targets
   `[UALSDb].[ualsdataview].[LayerData_82]` with geometry column
   `Geom`, **When** a tile is requested at a zoom level within range,
   **Then** only the curated attribute whitelist
   (`Id`, `LayerId`, `DISTRICT`, `SUBMUNICIPALITY`,
   `SUBDISTRICT_NAME`, `PARCELNUMBER`, `PARCEL_LANDUSE`,
   `LAND_USES`, `PLAN_NUMBER`, `BlockNumber`) appears in the tile.

3. **Given** the source table has 50+ columns including audit fields,
   concurrency stamps, and soft-delete metadata, **When** tiles are
   generated, **Then** none of the excluded columns appear in tile
   output.

---

### Edge Cases

- What happens when a tile coordinate has valid z/x/y format but
  falls outside the mathematically valid range for the zoom level?
  The system MUST return HTTP 400 with an error indicating invalid
  tile coordinates.

- What happens when a layer's data provider is unreachable (database
  connection failure)? The system MUST return HTTP 503 for that layer
  — not serve a stale tile or fail silently.

- What happens when cache disk space is exhausted? The system MUST
  log the failure, skip cache write, and still return the generated
  tile to the client.

- What happens when two concurrent requests trigger on-demand
  generation for the same tile? The system SHOULD ensure only one
  generation occurs; the second request SHOULD wait for or receive
  the same result rather than duplicating work.

- What happens when a cache replacement swap is triggered while a
  previous swap is still in progress? The system MUST reject the
  second request or queue it rather than corrupting cache state.

- What happens when a layer config file contains invalid JSON? The
  system MUST log a warning and skip the layer rather than crashing
  the application startup.

## Requirements *(mandatory)*

### Functional Requirements

**Library Integration**

- **FR-001**: System MUST be distributable as a reusable library that
  any ASP.NET Core host application can reference and configure.
- **FR-002**: System MUST expose a service registration entry point
  that the host calls during startup.
- **FR-003**: System MUST expose an endpoint mapping entry point that
  the host calls during pipeline configuration.
- **FR-004**: System MUST support configuration from the host
  application's standard configuration system (e.g., JSON settings
  files, environment variables).

**Tile Serving**

- **FR-005**: System MUST expose a public tile endpoint at a
  configurable route prefix, accepting layer ID, zoom level, x
  coordinate, and y coordinate, returning MVT/PBF binary output.
- **FR-006**: System MUST validate layer existence, layer enablement,
  and tile coordinate validity before processing a tile request.
- **FR-007**: System MUST return a valid empty MVT/PBF tile (not an
  error) when a layer exists but has no visible features for the
  requested tile and scope.
- **FR-008**: System MUST use XYZ tile addressing.
- **FR-009**: System MUST default to EPSG:3857 as the serving
  spatial reference unless explicitly overridden per layer.
- **FR-010**: System MUST default to MVT extent 4096 and tile buffer
  64 unless overridden per layer.

**Layer Configuration**

- **FR-011**: System MUST load layer definitions from individual
  configuration files, one file per layer.
- **FR-012**: Each layer configuration MUST specify: unique integer
  ID, unique layer key, display name, enabled flag, provider type
  and settings, geometry column, source SRID, serving SRID, zoom
  limits, tile extent, tile buffer, clipping behavior, cache rules,
  security scope filtering rules, and attribute whitelist.
- **FR-013**: System MUST support global default values for tile
  extent, tile buffer, serving SRID, and cache settings — individual
  layers may override these defaults.
- **FR-014**: Only attributes explicitly listed in the layer's
  whitelist MAY be emitted into tile output. Attribute whitelisting
  is mandatory, not optional.
- **FR-014a**: System MUST support reloading layer configurations at
  runtime via an explicit admin endpoint without requiring an
  application restart. The system MUST NOT use file-system watchers
  for automatic reload.

**Security**

- **FR-015**: System MUST enforce security server-side. Unauthorized
  records MUST NEVER be encoded into vector tiles.
- **FR-015a**: System MUST support a configurable global default for
  layer authentication posture. When a layer does not define its own
  security scope rules, the global default determines whether
  authentication is required. The recommended default is "require
  auth" (deny-by-default).
- **FR-016**: System MUST resolve security scope from authenticated
  user identity and trusted server-side policy, not from arbitrary
  client input.
- **FR-017**: System MUST support optional scope query parameters for
  advanced scenarios, but the normal path MUST derive scope from
  server-side policy.
- **FR-018**: Admin endpoints MUST require authorization. The host
  MUST be able to configure which roles have admin access.
- **FR-019**: Metadata endpoints MUST NOT expose connection strings,
  provider secrets, or internal security policy details.
- **FR-020**: All database queries MUST use parameterized SQL. No
  user-controlled input may be concatenated into executable SQL.

**Caching**

- **FR-021**: System MUST support disk-based tile caching using cache
  keys that include layer identity, tile coordinates (z/x/y), and
  resolved security scope.
- **FR-022**: System MUST support optional in-memory caching as a
  faster layer above disk cache.
- **FR-023**: Cache design MUST remain open to future distributed
  cache support without redesigning the core.
- **FR-024**: Cache invalidation MUST be explicit and triggered
  through admin endpoints or background jobs.
- **FR-024a**: The data change notification endpoint MUST accept a
  bounding box (envelope) from the caller. The system MUST compute
  the affected tile coordinates across all configured zoom levels
  for the layer and invalidate those cache entries. The caller is
  NOT required to provide tile coordinates or feature IDs.
- **FR-025**: System MUST support safe two-stage cache replacement:
  create new cache version, switch immediately, serve through new
  cache, generate content in background, delete old cache later.

**Background Operations**

- **FR-026**: System MUST support background jobs for cache
  generation, cache deletion, cache invalidation, and cache
  replacement workflows.
- **FR-027**: Background job dashboard MUST be available at a
  configurable path and MUST require authorization.
- **FR-028**: Background jobs MUST NOT block tile serving. The system
  MUST continue serving requests while background operations run.
- **FR-028a**: When a cache generation job fails mid-execution, the
  system MUST mark the job as failed in runtime settings, MUST
  retain any tiles already written (they remain valid and serveable),
  and MUST allow the operator to manually retry the job. The system
  MUST NOT discard partial cache output on failure.

**Runtime Settings**

- **FR-029**: System MUST persist runtime cache state (active cache
  folder/version, generation status, invalidation state) in a
  durable store.
- **FR-030**: Runtime settings MAY be cached in memory for fast
  lookup, but the durable store remains the source of truth.
- **FR-031**: If no external settings database is configured, the
  system MUST default to an embedded local database for runtime
  settings.

**Data Providers**

- **FR-032**: System MUST support multiple data provider types
  through a shared abstraction.
- **FR-033**: System MUST include SQL Server and Oracle providers in
  the first version.
- **FR-034**: Provider implementations MUST use low-overhead
  data access (raw SQL / ADO.NET or equivalent) for hot-path
  tile queries — not ORM entity tracking.
- **FR-035**: Providers MUST return features in a normalized form:
  feature ID, geometry, and whitelisted attributes.
- **FR-036**: The tile encoder MUST NOT have knowledge of specific
  database systems, authentication, or HTTP concerns.
- **FR-037**: Provider-specific concerns (connection, query dialect)
  MUST NOT leak into shared library components.

**MVT/PBF Encoding**

- **FR-038**: System MUST generate valid Mapbox Vector Tile-
  compatible PBF binary output.
- **FR-039**: Encoder MUST respect attribute whitelist — only
  explicitly allowed attributes appear in output.
- **FR-040**: Encoder MUST produce valid empty tiles when no features
  are present.

**Admin Endpoints**

- **FR-041**: System MUST expose admin endpoints for: triggering
  cache generation, cache deletion, cache invalidation, data change
  notification, cache version swap, cache status inspection, and
  layer configuration reload.
- **FR-042**: All admin endpoints MUST require authorization.

**Layer Metadata**

- **FR-043**: System MUST expose public endpoints for listing
  available (enabled) layers and retrieving individual layer
  metadata.
- **FR-044**: Metadata responses MUST include only frontend-safe
  information: layer ID, name, zoom range, and tile URL template.

**Cost Control**

- **FR-045**: System MUST support minimum and maximum zoom thresholds
  per layer to prevent expensive low-zoom requests.
- **FR-046**: System MUST support configurable behavior when a tile
  is outside the permitted zoom range (return empty tile vs. reject).
- **FR-047**: System SHOULD support optional restrictions on on-
  demand tile generation at certain zoom levels.

**Observability**

- **FR-048**: System MUST emit structured log entries for tile
  requests (layer, coordinates, scope, cache hit/miss, duration),
  errors, and background job lifecycle events. Logging MUST use
  the host's logging infrastructure (e.g., ILogger).
- **FR-049**: System MUST expose a health check endpoint at a
  configurable path that reports operational status (settings store
  reachable, cache folder accessible). The health check MUST
  integrate with the host's health check framework.

**Sample Application**

- **FR-050**: A sample host application MUST be included that
  consumes the library and demonstrates tile serving, provider
  configuration, layer configuration, and security integration.
- **FR-051**: The sample application MUST use a curated attribute
  whitelist — not the full source table schema.

### Key Entities

- **Layer**: A GIS dataset configured for tile serving. Defines
  source, provider, zoom range, attributes, security rules, and
  cache policy. Each layer has a unique integer ID and string key.

- **Security Scope**: An access control partition (e.g., "admin",
  "engineer", "public", or a district/organization key) resolved
  from authenticated user claims. Determines which features are
  visible and is part of the cache key.

- **Tile**: Identified by layer ID + z/x/y coordinates + security
  scope. The atomic unit of cached and served vector tile data.

- **Cache Entry**: Stored tile bytes keyed by layer, coordinates,
  scope, and cache version. Lives on disk and optionally in memory.

- **Runtime Settings**: Durable per-layer operational state: active
  cache version, generation status, invalidation state, timestamps.

- **Feature**: A single geospatial record returned by a provider:
  unique ID, geometry, and whitelisted attribute values. Intermediate
  representation between database and MVT encoder.

- **Provider**: A pluggable data source adapter that queries a
  database/service and returns normalized features. Each layer
  references one provider type and its connection settings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A host developer can add vector tile capabilities to
  an existing application and serve their first tile within 30
  minutes of reading the documentation.

- **SC-002**: Cached tile requests are served in under 50
  milliseconds under normal load (single server, standard hardware).

- **SC-003**: The system sustains at least 1,000 concurrent tile
  requests without degradation when tiles are cached.

- **SC-004**: 100% of tile output is server-side security-filtered —
  no unauthorized record is ever present in any served tile under
  any request pattern.

- **SC-005**: Cache replacement (swap to new version) completes
  without any interruption to tile serving — zero-downtime
  transition.

- **SC-006**: Adding a new data provider requires only implementing
  the provider abstraction — no changes to caching, security,
  encoding, or endpoint logic.

- **SC-007**: Tile payloads contain only whitelisted attributes —
  the ratio of emitted attributes to total source-table columns is
  intentionally low (e.g., 10 of 50+ columns in the sample layer).

- **SC-008**: Background cache generation for a layer runs to
  completion without blocking or degrading concurrent tile request
  handling.

## Assumptions

- The host application provides its own authentication middleware.
  VectorTileHub consumes authenticated user identity (claims
  principal) but does not implement login flows.

- Geometry data in source tables is stored in a recognized spatial
  format compatible with the provider's spatial query capabilities.

- The host application has file-system write access for disk cache
  storage at the configured cache root folder.

- Per-user cache segmentation is not required. Role/scope-level
  cache segmentation (e.g., "admin", "engineer", "public") is
  sufficient for the first version.

- Response compression for tile output is delegated to ASP.NET Core
  middleware or handled natively by the host — the library provides
  an opt-in flag but relies on standard middleware.

- The default internal settings database is a lightweight embedded
  database suitable for single-server deployment. Distributed
  settings coordination is out of scope for the first version.

- The host is responsible for configuring connection strings and
  secrets management. The library reads connection strings from
  configuration but does not implement secrets management.

- Oracle and SQL Server providers are the only required providers
  for the first version. PostGIS, SQLite/SpatiaLite, and other
  providers may be added later without redesigning the core.

- The map frontend is typically OpenLayers-based but the library
  remains frontend-agnostic — any client that speaks MVT/PBF over
  HTTP can consume the tile endpoint.
