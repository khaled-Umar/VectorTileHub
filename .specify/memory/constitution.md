<!--
Sync Impact Report
===================
Version change: 0.0.0 (template) -> 1.0.0
Bump rationale: MAJOR - initial adoption of constitution from project seed

Modified principles: N/A (initial version)

Added sections:
  - Project Identity & Mission (preamble)
  - Core Principles: I. Library-First Rule, II. Separation of Concerns,
    III. Policy-Driven Request Handling, IV. Security, V. Provider Independence,
    VI. Performance
  - Technical Standards: Configuration, Tile Output, Cache, SQL,
    Runtime Settings
  - Delivery Standards: Sample Application, First-Version Discipline,
    Decision Order, Enforcement, Final Principle
  - Governance

Removed sections: None

Templates requiring updates:
  - .specify/templates/plan-template.md       => OK (Constitution Check section is generic)
  - .specify/templates/spec-template.md       => OK (no constitution-specific references)
  - .specify/templates/tasks-template.md      => OK (no constitution-specific references)
  - .specify/templates/commands/              => N/A (no command files present)

Follow-up TODOs: None
-->

# VectorTileHub Constitution

## Project Identity & Mission

- **Product name**: `VectorTileHub`
- **Namespace**: `K1Soft.IT.VectorTileHub`
- **Primary platform**: .NET / ASP.NET Core / C#
- **Primary artifact**: reusable infrastructure library
- **Secondary artifact**: sample ASP.NET Core application demonstrating
  real integration

`VectorTileHub` MUST provide a reusable, secure, and high-performance
vector tile serving library for ASP.NET Core applications.

The project MUST enable host applications to:

- Expose MVT/PBF tile endpoints
- Configure layers from external configuration
- Enforce server-side security filtering
- Support cache-aware tile serving
- Support multiple data providers through stable abstractions

## Core Principles

### I. Library-First Rule

The library is the product. The sample application is only a consumer
and proof of integration.

- Core capabilities MUST NOT be implemented only inside the sample
  application.
- If behavior is essential to tile serving, security, caching, provider
  abstraction, runtime settings, or background jobs, it MUST live in
  the reusable library.

### II. Separation of Concerns

The system MUST preserve clear boundaries between:

- HTTP/API endpoint handling
- Tile orchestration
- Security scope resolution
- Cache management
- Runtime settings
- Layer configuration loading
- Feature retrieval
- MVT/PBF encoding
- Background job execution

No component may take ownership of responsibilities that belong to
another layer without explicit architectural justification.

### III. Policy-Driven Request Handling

Every tile request MUST be treated as a policy decision, not merely a
database query.

For each request, the system MUST determine:

1. Who is requesting
2. Which layer is requested
3. Which tile is requested
4. Which security scope applies
5. Which provider serves the layer
6. Whether cached output is valid
7. Whether to return cached output, generate output, or return an
   empty tile

### IV. Security (NON-NEGOTIABLE)

Security MUST be enforced server-side. The following rules are
non-negotiable:

- Unauthorized records MUST NEVER be encoded into vector tiles
- The frontend MUST NEVER be trusted to hide restricted data
- Attribute exposure MUST be whitelist-based
- Admin endpoints MUST require authorization
- Scope resolution MUST come from trusted server-side policy
- User-controlled input MUST NEVER be concatenated into executable SQL

If a design is convenient but risks leaking unauthorized data, it MUST
be rejected.

### V. Provider Independence

Data providers MUST be replaceable. The architecture MUST ensure:

- The encoder does not know about SQL Server, Oracle, files,
  authentication, or HTTP
- Providers do not own cache policy or endpoint behavior
- Shared abstractions remain provider-agnostic
- New providers can be added without redesigning the core

### VI. Performance

Performance is a core requirement, not a later optimization.

The project MUST prefer designs that:

- Keep the hot path lean
- Avoid unnecessary allocations and over-abstraction
- Minimize expensive database and filesystem work per request
- Support high request volume predictably

Hot-path tile querying SHOULD use raw SQL / ADO.NET or equivalent
low-overhead access.

EF Core MAY be used for runtime settings, metadata, and administrative
persistence, but MUST NOT be used as the main hot-path tile query
mechanism.

## Technical Standards

### Configuration

The system MUST be configuration-driven. The following MUST be
externally configurable:

- Route prefix
- Global VectorTileHub settings
- Layer definitions
- Cache root folders
- Provider settings
- Runtime settings store
- Security policy mappings
- Background job behavior
- Dashboard authorization

Host-specific assumptions MUST NOT be hardcoded into the reusable core.

### Tile Output

The system serves Mapbox Vector Tile / MVT / PBF output. The following
rules apply:

- XYZ tile addressing MUST be used
- Serving SRID defaults to `EPSG:3857` unless explicitly overridden
- MVT extent defaults to `4096`
- Tile buffer defaults to `64`
- Only explicitly whitelisted attributes MAY be emitted
- Tile payloads SHOULD remain lightweight and rendering-focused

The project MUST prefer minimal tile payloads over exposing full
source-table schemas.

### Cache

Caching is a first-class behavior. The cache design MUST:

- Include layer identity in cache keys
- Include tile coordinates in cache keys
- Include resolved scope in cache keys
- Support disk cache
- Allow memory cache as an optional layer
- Remain open to future distributed cache support

Cache invalidation MUST be explicit and operationally understandable.

**Cache Replacement Rule**: The system MAY switch immediately to a new
empty cache folder/version. After the switch:

- The server serves through the new active cache
- Cache content MAY be rebuilt in the background
- The old cache MAY be deleted later in the background

The system MUST NOT delay cutover merely to finish deleting the old
cache. Rationale: deleting a large cache with many small files can
reduce performance and delay availability.

### SQL

All SQL MUST be parameterized. The project MUST NOT:

- Concatenate user-controlled values into SQL
- Trust raw client filter fragments
- Embed unsafe provider-specific string generation on the hot path

Provider-specific SQL templates are acceptable only when combined with
trusted server-side construction and strict parameterization.

### Runtime Settings

Runtime cache and operational settings MUST be stored durably. The
system MUST track enough information to support:

- Active cache folder/version
- Cache generation state
- Cache invalidation state
- Background job coordination
- Operational inspection

Runtime settings MAY be cached in memory for fast lookup, but durable
state remains the source of truth.

## Delivery Standards

### Sample Application

A sample ASP.NET Core application MUST be included to prove library
integration in a realistic host environment.

The sample application MUST:

- Consume the reusable library as an external host would
- Demonstrate provider registration and configuration
- Demonstrate layer configuration
- Demonstrate tile endpoint usage
- Demonstrate SQL Server integration

For the current project context, the sample application SHOULD
demonstrate integration against:

- Connection string name: `Default`
- Database: `UALSDb`
- Table/view source: `[UALSDb].[ualsdataview].[LayerData_82]`
- Geometry column: `Geom`

The sample application MUST NOT expose all attributes from the source
schema. It MUST use a curated whitelist focused on rendering and
lightweight inspection only. Audit fields, concurrency fields, large
text fields, soft-delete metadata, and unrelated internal columns
SHOULD be excluded from vector tile output by default.

### First-Version Discipline

The first version MUST focus on:

- Reusable library design
- SQL Server support
- Oracle support
- Secure tile serving
- Cache-aware behavior
- Background cache workflows
- Sample host integration

The first version SHOULD avoid:

- Overbuilt administration UI
- Unnecessary distributed complexity
- Provider-specific leakage into shared layers
- Exposing too many attributes in tiles
- Turning the sample app into the real implementation

### Decision Order

When tradeoffs are necessary, decisions MUST prioritize:

1. Security
2. Correctness
3. Reusability of the library
4. Operational performance
5. Extensibility

Convenience is not a valid reason to violate security, architecture,
or reusability.

### Enforcement

A design, specification, or implementation violates this constitution
if it:

- Hardcodes host-specific assumptions into the core library
- Leaks unauthorized data into tiles
- Relies on client-side filtering for security
- Collapses provider, cache, and HTTP concerns into one layer
- Exposes the full database schema by default
- Weakens provider replaceability
- Turns the sample application into the real home of core logic

### Final Principle

`VectorTileHub` MUST be built as a policy-driven, provider-agnostic,
cache-aware vector tile engine for ASP.NET Core.

All specifications and implementations MUST be evaluated against that
principle.

## Governance

This constitution supersedes all other project practices where
conflicts arise.

**Amendment procedure**:

1. Amendments MUST be documented with rationale
2. Amendments MUST update the constitution version
3. All dependent templates MUST be checked for consistency after
   amendment
4. A Sync Impact Report MUST be produced for each amendment

**Versioning policy**:

- MAJOR: backward-incompatible governance/principle removals or
  redefinitions
- MINOR: new principle/section added or materially expanded guidance
- PATCH: clarifications, wording, typo fixes, non-semantic refinements

**Compliance review**:

- All PRs and reviews MUST verify compliance with this constitution
- Complexity beyond what is specified MUST be justified against the
  Decision Order
- Violations identified during review MUST be resolved before merge

**Version**: 1.0.0 | **Ratified**: 2026-05-24 | **Last Amended**: 2026-05-24
