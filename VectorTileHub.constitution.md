# VectorTileHub Constitution

This constitution defines the mandatory engineering principles, architectural boundaries, and delivery rules for the `VectorTileHub` project.

It is intended to guide specification generation, implementation decisions, and review criteria.

## 1. Project Identity

- Product name: `VectorTileHub`
- Namespace: `K1Soft.IT.VectorTileHub`
- Primary platform: `.NET / ASP.NET Core / C#`
- Primary artifact: reusable infrastructure library
- Secondary artifact: sample ASP.NET Core application that demonstrates real integration

## 2. Mission

`VectorTileHub` must provide a reusable, secure, and high-performance vector tile serving library for ASP.NET Core applications.

The project must enable host applications to:

- expose MVT/PBF tile endpoints
- configure layers from external configuration
- enforce server-side security filtering
- support cache-aware tile serving
- support multiple data providers through stable abstractions

## 3. Library-First Rule

The library is the product.

The sample application is only a consumer and proof of integration.

Core capabilities must not be implemented only inside the sample application.

If behavior is essential to tile serving, security, caching, provider abstraction, runtime settings, or background jobs, it must live in the reusable library.

## 4. Separation of Concerns

The system must preserve clear boundaries between:

- HTTP/API endpoint handling
- tile orchestration
- security scope resolution
- cache management
- runtime settings
- layer configuration loading
- feature retrieval
- MVT/PBF encoding
- background job execution

No component may take ownership of responsibilities that belong to another layer without explicit architectural justification.

## 5. Policy-Driven Request Handling

Every tile request must be treated as a policy decision, not merely a database query.

For each request, the system must determine:

- who is requesting
- which layer is requested
- which tile is requested
- which security scope applies
- which provider serves the layer
- whether cached output is valid
- whether to return cached output, generate output, or return an empty tile

## 6. Security Constitution

Security must be enforced server-side.

The project must obey the following rules:

- unauthorized records must never be encoded into vector tiles
- the frontend must never be trusted to hide restricted data
- attribute exposure must be whitelist-based
- admin endpoints must require authorization
- scope resolution must come from trusted server-side policy
- user-controlled input must never be concatenated into executable SQL

If a design is convenient but risks leaking unauthorized data, it must be rejected.

## 7. Provider Independence

Data providers must be replaceable.

The architecture must ensure:

- the encoder does not know about SQL Server, Oracle, files, authentication, or HTTP
- providers do not own cache policy or endpoint behavior
- shared abstractions remain provider-agnostic
- new providers can be added without redesigning the core

## 8. Performance Constitution

Performance is a core requirement, not a later optimization.

The project must prefer designs that:

- keep the hot path lean
- avoid unnecessary allocations and over-abstraction
- minimize expensive database and filesystem work per request
- support high request volume predictably

Hot-path tile querying should use raw SQL / ADO.NET or equivalent low-overhead access.

EF Core may be used for runtime settings, metadata, and administrative persistence, but not as the main hot-path tile query mechanism.

## 9. Configuration Constitution

The system must be configuration-driven.

The following must be externally configurable:

- route prefix
- global VectorTileHub settings
- layer definitions
- cache root folders
- provider settings
- runtime settings store
- security policy mappings
- background job behavior
- dashboard authorization

Host-specific assumptions must not be hardcoded into the reusable core.

## 10. Tile Output Constitution

The system serves Mapbox Vector Tile / MVT / PBF output.

The project must follow these output rules:

- XYZ tile addressing must be used
- serving SRID defaults to `EPSG:3857` unless explicitly overridden
- MVT extent defaults to `4096`
- tile buffer defaults to `64`
- only explicitly whitelisted attributes may be emitted
- tile payloads should remain lightweight and rendering-focused

The project must prefer minimal tile payloads over exposing full source-table schemas.

## 11. Cache Constitution

Caching is a first-class behavior.

The cache design must:

- include layer identity in cache keys
- include tile coordinates in cache keys
- include resolved scope in cache keys
- support disk cache
- allow memory cache as an optional layer
- remain open to future distributed cache support

Cache invalidation must be explicit and operationally understandable.

### Cache Replacement Rule

The system may switch immediately to a new empty cache folder/version.

After the switch:

- the server serves through the new active cache
- cache content may be rebuilt in the background
- the old cache may be deleted later in the background

The system must not delay cutover merely to finish deleting the old cache.

This rule exists because deleting a large cache with many small files can reduce performance and delay availability.

## 12. SQL Constitution

All SQL must be parameterized.

The project must not:

- concatenate user-controlled values into SQL
- trust raw client filter fragments
- embed unsafe provider-specific string generation on the hot path

Provider-specific SQL templates are acceptable only when combined with trusted server-side construction and strict parameterization.

## 13. Runtime Settings Constitution

Runtime cache and operational settings must be stored durably.

The system must track enough information to support:

- active cache folder/version
- cache generation state
- cache invalidation state
- background job coordination
- operational inspection

Runtime settings may be cached in memory for fast lookup, but durable state remains the source of truth.

## 14. Sample Application Constitution

A sample ASP.NET Core application must be included to prove that the library works in a realistic host environment.

The sample application must:

- consume the reusable library as an external host would
- demonstrate provider registration and configuration
- demonstrate layer configuration
- demonstrate tile endpoint usage
- demonstrate SQL Server integration

For the current project context, the sample application should demonstrate integration against:

- connection string name: `Default`
- database: `UALSDb`
- table/view source: `[UALSDb].[ualsdataview].[LayerData_82]`
- geometry column: `Geom`

The sample application must not expose all attributes from the source schema.

It must use a curated whitelist focused on rendering and lightweight inspection only.

Audit fields, concurrency fields, large text fields, soft-delete metadata, and unrelated internal columns should be excluded from vector tile output by default.

## 15. First-Version Discipline

The first version must focus on:

- reusable library design
- SQL Server support
- Oracle support
- secure tile serving
- cache-aware behavior
- background cache workflows
- sample host integration

The first version should avoid:

- overbuilt administration UI
- unnecessary distributed complexity
- provider-specific leakage into shared layers
- exposing too many attributes in tiles
- turning the sample app into the real implementation

## 16. Decision Order

When tradeoffs are necessary, decisions must prioritize:

1. security
2. correctness
3. reusability of the library
4. operational performance
5. extensibility

Convenience is not a valid reason to violate security, architecture, or reusability.

## 17. Enforcement

A design, specification, or implementation violates this constitution if it:

- hardcodes host-specific assumptions into the core library
- leaks unauthorized data into tiles
- relies on client-side filtering for security
- collapses provider, cache, and HTTP concerns into one layer
- exposes the full database schema by default
- weakens provider replaceability
- turns the sample application into the real home of core logic

## 18. Final Principle

`VectorTileHub` must be built as a policy-driven, provider-agnostic, cache-aware vector tile engine for ASP.NET Core.

All specifications and implementations should be evaluated against that principle.
