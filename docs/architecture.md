# Architecture

VectorTileHub is designed as a library-first ASP.NET Core component.

## Request Pipeline

```text
HTTP endpoint
  -> VectorTileOrchestrator
  -> Security scope resolver
  -> Runtime settings store
  -> Cache
  -> Feature provider
  -> MVT encoder
```

## Rules

- HTTP endpoints do not query databases directly.
- Providers do not know about HTTP or cache policy.
- The encoder does not know about SQL Server, Oracle, authentication, or disk cache.
- Cache keys include layer, tile coordinates, security scope, and cache version.
- Attribute output is whitelist-based.

## Main Extension Points

- `IVectorTileFeatureProvider`: implement a new data provider.
- `IVectorTileSecurityScopeResolver`: customize role and scope resolution.
- `IVectorTileCache`: replace or extend cache behavior.
- `IVectorTileRuntimeSettingsStore`: replace SQLite runtime settings persistence.
