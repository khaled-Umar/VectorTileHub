# Research: VectorTileHub — Host-Agnostic Vector Tile Server Library

**Date**: 2026-06-01
**Branch**: `002-vector-tile-server`

This builds on the validated 001 research and records the decisions that
differ for 002 (host-owned security, variant keys, stale-while-revalidate,
.NET 10 / `.slnx`, and the OpenLayers + SLD-style sample).

## R1: MVT/PBF Encoding in .NET (carried from 001)

**Decision**: Use `NetTopologySuite.IO.VectorTiles` (Mapbox) for MVT
encoding from NTS `Geometry` + attributes.

**Rationale**: NTS is the project's geometry library; the MVT extension
emits valid PBF with configurable extent/buffer/clipping, avoiding manual
protobuf work.

**Alternatives considered**: hand-rolled protobuf (full control, high
effort — reserved as fallback); MapboxTileCS (less maintained, no NTS
integration).

## R2: Spatial Query Patterns + Variant Filtering

**Decision**: Providers run **parameterized** bounding-box spatial queries
via ADO.NET, returning WKB + whitelisted attributes. The **variant filter**
(from the layer's selected cache rule) is applied **server-side** as an
additional parameterized predicate built from a trusted, server-defined
template — never from client-supplied SQL.

**Rationale**: WKB keeps providers decoupled from NTS internals. Applying
the variant filter inside the provider query guarantees unauthorized rows
for that variant are never read or encoded (preserves the non-negotiable
data-leak protection even though the *choice* of variant is made by the
host).

**SQL Server pattern** (variant filter appended as parameterized predicate):
```sql
SELECT [Id], [Geom].STAsBinary() AS GeomWkb, [Attr1], [Attr2]
FROM [Schema].[View]
WHERE [Geom].STIntersects(geometry::STGeomFromWKB(@envelope, @srid)) = 1
  AND (/* variant predicate, e.g. */ [Type_t] IN (@v0, @v1));
```

**Oracle pattern**: `SDO_RELATE(... 'mask=ANYINTERACT')` plus the same
parameterized variant predicate via bind variables.

**Alternatives considered**: returning NTS geometry directly (hard NTS
dependency in providers); GeoJSON (verbose/slow). Both rejected as in 001.

## R3: Tile Math (XYZ ↔ bounds, bbox coverage) (carried from 001)

**Decision**: Static `TileCoordinateUtils` implementing Slippy-Map XYZ ↔
EPSG:3857 conversion, buffer expansion, and **bbox → tile-coverage** across
a zoom range (used by the notify-change endpoint to compute affected tiles).

**Rationale**: Well-known compact formulas; no external dependency.

**Alternatives considered**: ProjNet4GeoAPI — overkill for the two fixed
projections.

## R4: Cache Disk Structure (variant replaces scope)

**Decision**: Hierarchical folders keyed by **variant** instead of security
scope:
```
{CacheRoot}/{LayerId}/{VariantKey}/{CacheVersion}/{z}/{x}/{y}.pbf
```
Composite lookup key: `{LayerId}:{VariantKey}:{CacheVersion}:{z}:{x}:{y}`.

**Rationale**: Variant isolation (different policies never share tiles),
version isolation for **blue/green** swaps (delete an old version folder as
a unit), and z/x/y fan-out to avoid single-directory bottlenecks. The
default variant uses a reserved key (e.g. `default`).

**Alternatives considered**: flat filenames (single huge directory — slow);
DB-backed blob cache (higher latency — reserved for future distributed
cache).

## R5: Hangfire Integration + Host-Supplied Dashboard Authorization

**Decision**: Use Hangfire for fire-and-forget + continuation jobs. The
library registers job classes and exposes a dashboard-mounting extension
that accepts a **host-supplied `IDashboardAuthorizationFilter`** (or a
delegate). The library enforces **no built-in dashboard policy**.

**Rationale**: De-facto .NET job library with persistence, retries, and a
dashboard. Delegating authorization to the host matches 002's
no-security-in-library direction while still letting the host lock the
dashboard down.

**Job topology**:
- Generate: enqueue per-layer/variant generation.
- On-demand fill: synchronous generate-then-persist on cache miss (request
  path), de-duplicated so concurrent misses don't double-write.
- Notify: bbox → tile coverage → background refresh job.
- Stale-while-revalidate: serve stale, enqueue a single background refresh
  per tile (guarded against duplicates).
- Blue/green swap: **job A** builds a fresh cache into a new empty version
  folder and flips the active-version setting; **job B** deletes the old
  version folder afterward — two separate jobs so cutover never waits on
  deletion.

**Alternatives considered**: Quartz.NET (heavier); `BackgroundService`
(no persistence/dashboard/retry — insufficient).

## R6: Runtime Settings Store (write-through + memory mirror)

**Decision**: EF Core + SQLite by default (or a host-supplied connection)
for durable runtime settings, fronted by an in-memory mirror. Reads hit
memory; writes go through to the database and refresh the mirror
atomically. Durable store remains source of truth.

**Schema** (per-layer/variant operational state): active cache version,
generation status (Idle/Running/Failed), running job id, last
started/completed/invalidated timestamps, updated-at, extensible metadata.
Plus a global key/value row for the current active cache root path.

**Rationale**: SQLite needs no server and suits single-server v1; EF Core
gives migrations for low-volume settings (off the hot path). Memory mirror
satisfies FR-028/FR-029 (fast reads, write-through).

**Alternatives considered**: Dapper-on-SQLite (loses migrations); JSON file
(unsafe under concurrent job/request access).

## R7: Variant Resolution (replaces 001 security scope resolution)

**Decision**: Introduce `IVectorTileVariantResolver`. A tile request carries
an optional **variant key**. The default resolver maps the key to one of the
layer's configured **cache rules** (each rule = variant key + parameterized
filter). No key → the layer's designated **default** variant. Unknown key →
a clear "variant not found" result. The resolver does **no authentication or
authorization** — the host maps user role → variant key before calling.

**Rationale**: Cleanly separates *policy decision* (host) from *mechanism*
(library applies the chosen variant's server-side filter). Keeps the library
identity-provider-agnostic, satisfying the 002 direction while preserving
server-side filtering.

**Alternatives considered**:
- Keep 001's `ClaimsPrincipal`-based scope resolver — rejected: re-imposes
  an identity coupling the spec explicitly removes.
- Separate layer id per variant — rejected: multiplies layer management and
  loses the single-layer/variant-key model the spec clarified.

## R8: Stale-While-Revalidate Serving

**Decision**: On a cache hit where tile age exceeds the layer's refresh
period, serve the cached (stale) tile immediately and enqueue **one**
background refresh for that tile. A pending-refresh guard (in-memory set
keyed by cache key) prevents duplicate refresh jobs for the same tile.

**Rationale**: Keeps the request path fast and off the source database
(FR-022, spec clarification). Map tiles tolerate a brief staleness window.

**Alternatives considered**: block-and-regenerate (slower request path,
DB on hot path); configurable-per-layer (deferred — adds config/test
surface without current need; the resolved clarification chose a single
global behavior).

## R9: Health Check (carried from 001, provider-agnostic)

**Decision**: `IHealthCheck` verifying (1) settings store reachable,
(2) cache root accessible/writable, (3) layer-config folder readable.
Provider/database connectivity is the host's concern (connection strings
are host/layer-managed).

**Rationale**: Covers the library's own infrastructure without coupling to
host-managed databases.

## R10: .NET 10 + `.slnx` Solution Format

**Decision**: Target **.NET 10.0 / C# 14** and use the **`.slnx`** XML
solution format (SDK 10.0.300 is installed and supports `dotnet sln`
operations on `.slnx`). Replace the existing `VectorTileHub.sln` with
`VectorTileHub.slnx`.

**Rationale**: Explicit user requirement. `.slnx` is the modern,
merge-friendly solution format supported by the .NET 10 CLI and Visual
Studio; .NET 10 is an LTS release available as of this date.

**Migration note**: `dotnet sln VectorTileHub.sln migrate` produces a
`.slnx`; verify all 8 `src/` projects + 3 test projects are listed, then
remove the old `.sln`.

**Alternatives considered**: keep `.sln` — rejected (explicit requirement
for `.slnx`).

## R11: SLD → OpenLayers Style (sample only)

**Decision**: In the **sample project**, generate a render style from
`tmp/layerStyle.sld` and apply it in **OpenLayers** via
**`ol-mapbox-style`** consuming a **Mapbox GL JSON** style. A small
converter (`Tools/SldToStyleConverter.cs`) parses the SLD's `se:Rule`
elements and emits one GL `fill`/`line` layer per rule plus a `symbol`
layer for the scale-limited parcel label.

**SLD structure observed** (`tmp/layerStyle.sld`):
- 39 `se:Rule` elements; 38 `PolygonSymbolizer` rules keyed on the `Type_t`
  attribute (some via `ogc:Or` of multiple `Literal` values), each with a
  fill color, stroke color, stroke width, and `stroke-linejoin`.
- 1 `TextSymbolizer` label rule (`PARCELNUMBER` / `SERVICE_NAME`) gated by
  `se:MaxScaleDenominator` (2500) → mapped to a `minzoom` in the GL style.

**Mapping**:
| SLD construct | GL style output |
|---------------|-----------------|
| `PropertyIsEqualTo Type_t = X` | layer `filter`: `["==", "Type_t", "X"]` |
| `ogc:Or` of equals | `filter`: `["in", "Type_t", ...values]` |
| `Fill/SvgParameter fill` | `fill-color` |
| `Stroke/SvgParameter stroke` + width | companion `line` layer `line-color` / `line-width` |
| `stroke-linejoin` | `line-join` |
| `TextSymbolizer` + `MaxScaleDenominator` | `symbol` layer + `minzoom` (scale→zoom conversion) |

**Rationale**: `ol-mapbox-style` is the standard, well-supported path to
render Mapbox Vector Tiles in OpenLayers with a declarative style; GL JSON
maps cleanly from the SLD's per-rule symbology. This keeps all rendering in
the sample (the library still only emits PBF).

**Alternatives considered**:
- Hand-written OpenLayers `StyleFunction` switching on `Type_t` — viable but
  more imperative and harder to validate rule-for-rule against the SLD.
- Server-side rendering of the SLD — explicitly out of scope (clarified: the
  library produces only PBF).

## Resolved unknowns

- Performance latency/throughput **targets** and data-volume/scale figures
  are intentionally deferred to load testing (spec Deferred items); they do
  not block design. No remaining `NEEDS CLARIFICATION` markers.
