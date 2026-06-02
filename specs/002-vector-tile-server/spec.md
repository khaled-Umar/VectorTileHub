# Feature Specification: VectorTileHub — Host-Agnostic Vector Tile Server Library

**Feature Branch**: `002-vector-tile-server`

**Created**: 2026-06-01

**Status**: Draft

**Input**: User description: "Reusable MVT/PBF tile server for ASP.NET Core, packaged as a single NuGet library that any host application can inject. Layers are configured via separate external files (each with its own database connection string, supporting Oracle and SQL Server), data access is separated from cache/PBF generation, caching is managed by background jobs (generate, on-demand fill, blue/green delete, notify-update by area), server settings are persisted in a database table (host-provided connection or auto-created SQLite) and mirrored in memory, background work runs on Hangfire with a dashboard, the library contains NO endpoint exposure or security/roles (the host owns that), every endpoint is documented, a sample project demonstrates each endpoint via Swagger, and a rendering style equivalent to the supplied `layerStyle.sld` is produced for use when rendering."

> **Note on relationship to `001-vector-tile-engine`**: This feature (002) is a separate specification created at the user's request. It revises the product direction so that the **library itself contains no security, roles, or endpoint-exposure concerns** (the host application owns all exposure and authorization) and adds an explicit **SLD-derived rendering style** requirement. Feature 001 remains in the repository as prior history.

## Clarifications

### Session 2026-06-01

- Q: How should the SLD-derived rendering be delivered — does the library render, or is rendering only in the sample? → A: The library produces only PBF tiles (no rendering, no style output, no change to the library). Rendering belongs to the **sample project**, which uses **OpenLayers** to render the served PBF tiles using a style **generated from** the supplied `layerStyle.sld`.
- Q: How does a caller select which filtered cache variant a tile request uses? → A: The caller passes a variant/filter key (matching one of the layer's cache rules) in the tile request; the host maps role → variant key before calling, so the library stays role-agnostic.
- Q: When a cached tile exceeds the configured cache age, how should the server respond? → A: Serve the stale tile immediately and enqueue a background refresh (stale-while-revalidate), so the request path stays fast and the next request gets fresh data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Serve Vector Tiles from a Configured Layer (Priority: P1)

A host-application developer references the VectorTileHub package, registers it during application startup, and points it at one layer configuration file. Their own controller/proxy forwards a tile request (layer id, zoom, x, y) to the library, and the library returns a valid MVT/PBF binary tile containing the layer's whitelisted features and attributes for that tile's area.

**Why this priority**: Producing a valid vector tile from a data source is the core reason the library exists. Without it, nothing else has value. It is the minimum viable product.

**Independent Test**: Configure a single layer that points at one database table, request a tile at a coordinate known to contain features, and verify the response is a non-empty, well-formed MVT/PBF binary with the expected layer name and attributes; request a tile outside the configured zoom range and verify an empty/blank tile is returned.

**Acceptance Scenarios**:

1. **Given** a registered layer with data at zoom 14, **When** a request for that layer at zoom 14 / valid x,y is made, **Then** a valid MVT/PBF binary tile containing the layer's features is returned.
2. **Given** a layer configured with `minZoom=10` and `maxZoom=18`, **When** a tile is requested at zoom 5, **Then** an empty/blank tile (not an error) is returned.
3. **Given** a tile coordinate covering an area with no features, **When** the tile is requested, **Then** an empty/blank valid tile is returned rather than an error.
4. **Given** a request referencing a layer id that is not registered, **When** the tile is requested, **Then** a clear "layer not found" result is returned to the caller.

---

### User Story 2 — Configure Layers via External Files with Independent Connections (Priority: P1)

An operator defines each layer in its own configuration file placed anywhere on disk. Each layer file carries a unique integer id, layer name, its own database connection string and credentials, the source provider type (Oracle or SQL Server), the cache folder, cache rules, zoom serving range, and cache age/refresh period. At startup the host tells the library where each layer configuration file lives, and the library loads and validates them.

**Why this priority**: A tile cannot be served without a resolved layer definition, and the product requirement is that connection strings and credentials live per-layer (not in a shared appsettings block). This is co-essential with US1 for a working MVP.

**Independent Test**: Place two layer files in different folders — one targeting SQL Server, one targeting Oracle — register both by file path, start the library, and verify both layers resolve, validate, and become individually addressable by their integer ids.

**Acceptance Scenarios**:

1. **Given** two layer files in separate directories with distinct connection strings, **When** the library is initialized pointing at both file paths, **Then** both layers load and each is callable by its unique integer id.
2. **Given** a layer file declaring provider "Oracle" and another declaring "SqlServer", **When** tiles are requested from each, **Then** each query is executed against its own connection using the matching provider.
3. **Given** a layer file with a missing required field (e.g., no connection string), **When** the library loads it, **Then** a clear validation error identifying the file and field is surfaced and that layer is not served.
4. **Given** two layer files declaring the same integer id, **When** the library loads them, **Then** a duplicate-id conflict is reported.

---

### User Story 3 — Manage the Tile Cache Lifecycle (Priority: P2)

An operator (through the host's own administrative surface) instructs the library to (a) start generating a layer's cache, (b) fill missing tiles on demand when an uncached area is requested, (c) refresh the tiles intersecting a notified area, and (d) replace a layer's cache safely. All of these run as background jobs scoped to a specific layer.

**Why this priority**: Caching is what makes the server fast and is a major part of the requirements, but tiles can still be served live (US1) without it, so it ranks below the core serving path.

**Independent Test**: Trigger cache generation for a layer and verify tiles appear in the configured cache folder; request an uncached tile and verify it is generated, returned, and persisted; submit a bounding box to the notify endpoint and verify the intersecting tiles across zoom levels are recomputed; trigger a cache replacement and verify the old cache is removed only after the new cache is fully built.

**Acceptance Scenarios**:

1. **Given** a layer with no cache, **When** cache generation is started, **Then** a background job produces tiles into the layer's cache folder and progress is observable.
2. **Given** a request for a tile not present in the cache, **When** the tile is requested, **Then** it is generated on demand, returned to the caller, and saved for future requests.
3. **Given** a notify-change request carrying a bounding box, **When** it is received, **Then** the system computes the affected tile coordinates across the layer's zoom range and refreshes only those tiles as a background job.
4. **Given** a cache-replacement request, **When** it runs, **Then** one background job builds a fresh cache into a new empty folder and a second background job deletes the previous cache, so serving is never interrupted and partially built tiles are never served.
5. **Given** a background cache job that fails mid-run, **When** the failure occurs, **Then** the job is marked failed, already-written tiles remain usable, and the job can be retried.

---

### User Story 4 — Persist and Fast-Serve Server Settings (Priority: P2)

The library keeps server-wide settings (such as the current active cache folder path and other operational values) in a database table. The host may supply the connection for this settings store via app configuration; if none is supplied, the library creates and uses a local SQLite database. Settings are held in an in-memory cache for fast reads and are written through to the database (and refreshed in memory) whenever they change.

**Why this priority**: Settings persistence underpins blue/green cache swapping (US3) and consistent restarts, but the core serving path can run with defaults, so it is P2.

**Independent Test**: Start the library with no settings connection configured and verify a SQLite settings store is created and seeded; change the active cache folder and verify the value is persisted to the table and the in-memory copy reflects the new value without re-reading the database on every request.

**Acceptance Scenarios**:

1. **Given** no settings connection in host configuration, **When** the library starts, **Then** a SQLite settings database/table is created automatically and used.
2. **Given** a settings connection supplied by the host, **When** the library starts, **Then** the settings table is created/used in that database instead of SQLite.
3. **Given** a setting is read repeatedly, **When** it has not changed, **Then** reads are served from memory without additional database round-trips.
4. **Given** a setting is updated, **When** the update completes, **Then** both the database row and the in-memory copy reflect the new value.

---

### User Story 5 — Run Cache Work as Managed Background Jobs (Priority: P3)

All cache generation, refresh, replacement, and deletion run as background jobs managed by a job framework that provides a monitoring dashboard. Jobs are scoped per layer so operators can observe and manage them. The library does not impose who may view the dashboard; the host supplies any access control it wants.

**Why this priority**: Reliable background execution and observability are important operationally but build on the cache capabilities of US3, so they are P3.

**Independent Test**: Enqueue several per-layer cache jobs, open the dashboard, and verify each job is listed, attributable to its layer, and shows running/succeeded/failed state; verify the host can supply an authorization rule that governs dashboard access.

**Acceptance Scenarios**:

1. **Given** cache operations are triggered for a layer, **When** they run, **Then** each appears as a background job tagged to that layer with observable state.
2. **Given** the host provides a dashboard authorization rule, **When** the dashboard is accessed, **Then** the host's rule decides access (the library enforces no built-in policy).
3. **Given** multiple layers run jobs concurrently, **When** the dashboard is viewed, **Then** jobs are distinguishable by layer.

---

### User Story 6 — Scope Caches by Filter for Differing Data Policies (Priority: P3)

A layer can define cache rules that produce separate cache variants based on attribute filters applied to the source table, so that different consuming policies (e.g., different user roles, as decided and enforced by the host) receive tiles built from different subsets of the data. The library produces and addresses these filtered cache variants; it does not itself authenticate or authorize users.

**Why this priority**: Filtered cache variants extend the caching model for multi-policy scenarios but are not required for a single-audience deployment, so P3.

**Independent Test**: Configure a layer with two filter-based cache rules, generate caches, and verify each filtered variant is stored and addressable independently and contains only the rows matching its filter.

**Acceptance Scenarios**:

1. **Given** a layer with two filter-based cache rules, **When** caches are generated, **Then** two independent cache variants are produced, each reflecting its filter.
2. **Given** a tile request carrying a specific variant/filter key, **When** served, **Then** the returned tile contains only features matching that variant's filter; and **Given** no key, the layer's default variant is served.

---

### User Story 7 — Documented API plus a Sample Project that Renders Tiles in OpenLayers (Priority: P3)

The library produces only PBF tiles and does no rendering. Every endpoint the library offers is documented well enough to be called from another service, and a **sample host project** demonstrates each endpoint through interactive API documentation (Swagger) **and** includes an **OpenLayers** map page that renders the served PBF tiles using a style **generated from** the supplied `layerStyle.sld` — reproducing its symbology (land-use categories keyed on the `Type_t` attribute, with matching fills/strokes and a scale-limited parcel label).

**Why this priority**: Good documentation and a working render demo greatly improve adoption, but neither is required for the library to produce a tile, so P3.

**Independent Test**: Open the sample project's API documentation and verify every endpoint is listed with request/response descriptions and is callable; open the sample's OpenLayers page and verify it requests PBF tiles from the library and renders them with a style generated from `layerStyle.sld` that reproduces each rule's filter and symbology (fill, stroke width, stroke join) and the scale-limited label.

**Acceptance Scenarios**:

1. **Given** the sample project, **When** its API documentation is opened, **Then** every library endpoint is listed with a description, parameters, and example responses, and can be invoked interactively.
2. **Given** the sample's OpenLayers page, **When** it loads a layer, **Then** it fetches PBF tiles from the library and renders them in the browser.
3. **Given** the style generated from the supplied `layerStyle.sld`, **When** the sample renders, **Then** each source rule has an equivalent style rule (matching the `Type_t` value(s)) with the same fill color, stroke color, stroke width, and stroke join.
4. **Given** the parcel label rule with a maximum display scale, **When** the sample renders, **Then** the label (parcel number / service name) appears only within the equivalent scale/zoom limit.

---

### Edge Cases

- **Tile outside zoom range**: Requests below `minZoom` or above `maxZoom` return an empty/blank tile, never an error.
- **Empty area**: A valid tile covering no features returns an empty (but well-formed) tile.
- **Stale cache**: When a tile's cache age exceeds the layer's configured refresh period, the stale tile is served immediately and a background refresh is enqueued; a duplicate refresh is not enqueued if one is already pending for that tile.
- **Concurrent on-demand generation**: Two simultaneous requests for the same uncached tile must not corrupt the cache or perform redundant duplicate writes.
- **Cache swap during read**: Serving must continue without interruption while a blue/green cache replacement is in progress; readers see either the old complete cache or the new complete cache, never a half-built one.
- **Unavailable data source**: If a layer's database is unreachable, the failure is reported clearly and does not crash the host application or other layers.
- **Invalid/missing layer file at startup**: A malformed or unreadable layer file is reported with its path and the specific problem, and other valid layers still load.
- **Notify with oversized bounding box**: A notify-change area that spans a very large region across many zoom levels is handled as background work rather than blocking the caller.
- **SLD rules with multiple literal values** (e.g., an `Or` of `Type_t` values) must map to a single equivalent style rule covering all listed values.

## Requirements *(mandatory)*

### Functional Requirements

#### Packaging & Integration
- **FR-001**: The system MUST be distributable as a single NuGet package that a host ASP.NET Core application can reference and register during startup.
- **FR-002**: The system MUST contain no endpoint exposure, no user authentication, and no role/authorization logic of its own; the host application is solely responsible for exposing and securing any surface (proxy or endpoint).
- **FR-003**: The system MUST allow the host to act as a proxy that forwards tile requests to the library, so the host can apply its own per-user/role filtering before calling in.

#### Configuration
- **FR-004**: The system MUST be configurable through application configuration (appsettings/JSON) for library-level options.
- **FR-005**: The system MUST load each layer from a separate configuration file whose location is provided by the host (the file may reside anywhere on disk).
- **FR-006**: Each layer configuration MUST include: a unique integer id, a layer name, its own database connection string and credentials, the data provider type, a cache folder, cache rules, a zoom serving range (min/max), and a cache age / refresh period.
- **FR-007**: The system MUST validate layer configurations on load and surface clear, file- and field-specific errors for missing or invalid values, without preventing other valid layers from loading.
- **FR-008**: The system MUST reject duplicate layer integer ids and report the conflict.
- **FR-009**: The system MUST allow a layer's identity and settings to be retrieved by its unique integer id.
- **FR-010**: The system MUST apply changed layer configuration only via an explicit host-triggered reload action (no automatic file watching).

#### Tile Serving
- **FR-011**: The system MUST produce valid MVT/PBF binary tiles for a requested layer id, zoom, and tile coordinates, with an optional variant/filter key selecting a filtered cache variant.
- **FR-012**: The system MUST include in each tile only the attributes a layer is configured to expose (attribute whitelisting).
- **FR-013**: The system MUST return an empty/blank valid tile (not an error) for requests outside the layer's zoom serving range or for areas with no features.
- **FR-014**: The system MUST return a clear "layer not found" result when an unknown layer id is requested.

#### Data Access Abstraction
- **FR-015**: The system MUST separate data access from cache/PBF generation so that additional data providers can be added in the future without changing the tile-generation logic.
- **FR-016**: The system MUST support Oracle and SQL Server as data providers, selected per layer by its configuration.

#### Caching
- **FR-017**: The system MUST support starting generation of a layer's cache as a background job.
- **FR-018**: The system MUST generate an uncached tile on demand when requested, return it to the caller, and persist it for future requests.
- **FR-019**: The system MUST refresh the tiles intersecting a notified area; the caller provides a bounding box and the system computes affected tile coordinates across the layer's zoom range, performed as a background job.
- **FR-020**: The system MUST replace a layer's cache using two background jobs — one that builds a fresh cache into a newly created empty folder and a separate one that deletes the previous cache — so that serving is never interrupted and partial caches are never served.
- **FR-021**: The system MUST support deleting an existing cache for a layer.
- **FR-022**: The system MUST treat a tile whose age exceeds the layer's configured refresh period as stale; on a stale hit it MUST serve the existing (stale) tile immediately and enqueue a background refresh so subsequent requests receive the regenerated tile (stale-while-revalidate).
- **FR-023**: The system MUST scope all cache jobs (generate, refresh, replace, delete) to a specific layer.
- **FR-024**: The system MUST support cache rules that produce independent, separately addressable filtered cache variants for a layer (e.g., for differing host-defined data policies), each containing only rows matching its filter. A caller selects a variant by passing its variant/filter key in the tile request; when no key is supplied the layer's default variant is served, and an unknown key yields a clear "variant not found" result.
- **FR-025**: The system MUST prevent corruption or redundant duplicate writes when the same uncached tile is requested concurrently.

#### Server Settings Store
- **FR-026**: The system MUST persist server-wide settings (including the current active cache folder path) in a database table.
- **FR-027**: The system MUST use a settings-store database connection supplied by the host if provided; otherwise it MUST automatically create and use a local SQLite database for the settings table.
- **FR-028**: The system MUST cache settings in memory for fast reads and avoid repeated database reads while settings are unchanged.
- **FR-029**: The system MUST write setting changes through to the database and refresh the in-memory copy whenever a setting changes.

#### Background Jobs & Observability
- **FR-030**: The system MUST execute cache generation, refresh, replacement, and deletion as managed background jobs.
- **FR-031**: The system MUST provide a background-job monitoring dashboard, with jobs attributable to their layer and showing running/succeeded/failed state.
- **FR-032**: The system MUST allow the host to supply the authorization rule that governs dashboard access, and MUST NOT enforce any built-in dashboard access policy.
- **FR-033**: When a background cache job fails mid-execution, the system MUST mark it failed, keep already-written tiles usable, and allow manual retry.
- **FR-034**: The system MUST emit structured logging for requests, errors, and cache hits/misses, and expose a health indicator suitable for monitoring.

#### Documentation & Sample
- **FR-035**: The library MUST produce only PBF tiles and MUST NOT perform rendering or emit a rendering style itself.
- **FR-036**: The system MUST document every endpoint it offers in sufficient detail to be called from another service (purpose, parameters, request/response shapes, and error results).
- **FR-037**: The system MUST include a sample host project that demonstrates every endpoint through interactive API documentation (Swagger).
- **FR-038**: The sample project MUST include an OpenLayers map page that requests PBF tiles from the library and renders them in the browser.
- **FR-039**: The sample project MUST render using a style **generated from** the supplied `layerStyle.sld`, reproducing each source rule's feature filter (keyed on the `Type_t` attribute, including rules that match multiple literal values) and its symbology (fill color, stroke color, stroke width, stroke line-join).
- **FR-040**: The sample's generated style MUST reproduce the parcel label rule (parcel number / service name) including its maximum-display-scale limit so the label appears only within the equivalent scale/zoom.

### Key Entities *(include if feature involves data)*

- **Layer**: A configured, individually addressable map data source. Key attributes: unique integer id, name, data provider type, own connection string/credentials, attribute whitelist, cache folder, cache rules, zoom serving range (min/max), cache age/refresh period.
- **Layer Configuration File**: An external file (locatable anywhere) describing one Layer; registered with the library by path.
- **Tile**: An MVT/PBF binary for a (layer, zoom, x, y) address; may be served live or from cache; carries an age used for staleness checks.
- **Cache**: The set of stored tiles for a layer, organized in a folder; may have multiple filtered variants; subject to blue/green replacement.
- **Cache Rule**: A definition that yields a cache variant, including a variant/filter key used to select it and any attribute filter applied to the source data. A layer designates one variant as its default.
- **Cache Job**: A background unit of work (generate, on-demand fill, refresh-by-area, replace, delete) scoped to one layer, with observable state and retry.
- **Server Settings**: Persisted operational values (e.g., active cache folder path) stored in a database table, mirrored in memory.
- **Settings Store**: The database (host-provided or auto-created SQLite) holding the Server Settings table.
- **Rendering Style (sample-only)**: A client-side style used by the sample project's OpenLayers page, generated from the supplied SLD, mapping `Type_t` categories to symbology and including the scale-limited parcel label. It is not produced or served by the library.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can add the package to an empty ASP.NET Core host and serve a valid vector tile from one configured layer in under 30 minutes using only the provided documentation and sample.
- **SC-002**: A configured layer can be added or swapped by pointing at its external configuration file without changing any other layer's configuration and without code changes.
- **SC-003**: Both an Oracle-backed layer and a SQL Server-backed layer can be served from the same host instance simultaneously, each using its own connection.
- **SC-004**: A previously cached tile is served from cache without querying the source database on the request path.
- **SC-005**: An uncached tile request is fulfilled on first request and served from cache (no regeneration) on subsequent requests for the same tile until it becomes stale.
- **SC-006**: A cache replacement completes with zero interruption to tile serving — every request during the swap returns either a fully old or fully new tile, never a partial one.
- **SC-007**: Settings reads do not hit the database while unchanged, and a settings change is reflected in both storage and memory immediately after the change completes.
- **SC-008**: Every cache operation is visible as a background job attributable to its layer in the dashboard, including its success/failure state.
- **SC-009**: A failed cache generation job leaves all already-written tiles usable and can be retried without manual cleanup.
- **SC-010**: The sample project's OpenLayers page renders the served PBF tiles using a style generated from the source SLD that reproduces 100% of its rules — each `Type_t` category renders with the same fill, stroke, stroke width, and join, and the parcel label respects the same scale limit.
- **SC-011**: Every endpoint is represented in the sample project's interactive API documentation and can be invoked there, with no undocumented endpoints.

## Assumptions

- **Host owns exposure and security**: Per the user's direction, the library exposes a callable API surface but performs no endpoint hosting, authentication, or authorization itself; the host wires up routing and applies any user/role filtering (including for the job dashboard).
- **Library produces only PBF; rendering lives in the sample**: The library performs no rendering and emits no style. The sample project renders the served PBF tiles client-side with OpenLayers using a style generated from the supplied SLD. Server-side rendering (raster or otherwise) is out of scope.
- **Tile addressing**: Standard web-mercator XYZ tile addressing (layer id, zoom, x, y) is assumed unless a layer specifies otherwise.
- **Attribute whitelist**: Each layer controls which source attributes are emitted into tiles; non-whitelisted attributes are never included.
- **Background job framework with dashboard**: A job framework providing a monitoring dashboard (e.g., Hangfire) satisfies FR-030–FR-033; the specific framework is an implementation choice deferred to planning.
- **Settings-store default**: When the host supplies no settings connection, a local SQLite database is created in a library-determined location and seeded with defaults.
- **Notify granularity**: Affected tiles for a notify-change request are derived from a caller-supplied bounding box across the layer's configured zoom range.
- **Source data has the `Type_t` (and parcel `PARCELNUMBER` / `SERVICE_NAME`) attributes** referenced by the supplied SLD; the rendering style maps to those attribute names as given in the SLD.
- **Configuration reload is explicit**: Layer/config changes take effect only via a host-triggered reload action, not via filesystem watching.
