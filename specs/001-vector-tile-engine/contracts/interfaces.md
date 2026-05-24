# Core Interface Contracts

## IVectorTileService

The primary entry point for tile requests. Called by the tile endpoint.

```csharp
public interface IVectorTileService
{
    Task<VectorTileResult> GetTileAsync(
        int layerId,
        int z,
        int x,
        int y,
        ClaimsPrincipal user,
        string? scopeOverride,
        CancellationToken cancellationToken);
}
```

**Responsibilities**: Orchestrates the full tile request pipeline —
validates layer, resolves scope, checks cache, delegates to provider,
encodes tile, writes cache.

## IVectorTileFeatureProvider

Implemented by each data provider (SQL Server, Oracle).

```csharp
public interface IVectorTileFeatureProvider
{
    string ProviderType { get; }

    Task<VectorTileFeatureBatch> GetFeaturesAsync(
        VectorTileFeatureQuery query,
        CancellationToken cancellationToken);
}
```

**Contract**:
- MUST return features with geometry in the serving SRID
- MUST return only whitelisted attributes
- MUST apply security scope filtering server-side
- MUST use parameterized SQL
- MUST NOT use ORM entity tracking on the hot path

## IVectorTileEncoder

Encodes normalized features into MVT/PBF bytes.

```csharp
public interface IVectorTileEncoder
{
    byte[] Encode(
        string mvtLayerName,
        IReadOnlyList<VectorTileFeature> features,
        VectorTileEncodingContext context);

    byte[] EncodeEmpty(
        string mvtLayerName,
        VectorTileEncodingContext context);
}
```

**Contract**:
- MUST produce valid Mapbox Vector Tile-compatible PBF output
- MUST respect extent, buffer, and clipping settings from context
- MUST include only attributes present on VectorTileFeature
- MUST NOT have knowledge of databases, providers, or HTTP
- `EncodeEmpty` MUST return a valid MVT with zero features

## IVectorTileCache

Abstraction for tile cache (disk, memory, or composite).

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

    Task RemoveByEnvelopeAsync(
        int layerId,
        Envelope boundingBox,
        int minZoom,
        int maxZoom,
        string? scopeKey,
        string cacheVersion,
        CancellationToken cancellationToken);
}
```

**Contract**:
- `GetAsync` returns null on miss
- `SetAsync` MUST be idempotent (overwrite existing)
- `RemoveByEnvelopeAsync` computes affected tile coordinates and
  removes matching cache entries
- Implementations MUST NOT throw on I/O failures for Set/Remove
  (log and continue)

## IVectorTileSecurityScopeResolver

Resolves the security scope for a tile request.

```csharp
public interface IVectorTileSecurityScopeResolver
{
    Task<VectorTileSecurityScope> ResolveAsync(
        VectorTileLayerConfig layer,
        ClaimsPrincipal user,
        string? scopeOverride,
        CancellationToken cancellationToken);
}
```

**Contract**:
- MUST derive scope from user claims and server-side policy
- `scopeOverride` is advisory; the resolver MUST validate it
  against the user's allowed scopes before accepting
- MUST return a scope with `IsAuthenticated = false` for anonymous
  users (which the orchestrator uses to enforce auth requirements)

## IVectorTileRuntimeSettingsStore

Durable store for per-layer runtime state.

```csharp
public interface IVectorTileRuntimeSettingsStore
{
    Task<VectorTileLayerRuntimeSettings?> GetLayerRuntimeSettingsAsync(
        int layerId,
        CancellationToken cancellationToken);

    Task UpsertLayerRuntimeSettingsAsync(
        VectorTileLayerRuntimeSettings settings,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorTileLayerRuntimeSettings>> GetAllAsync(
        CancellationToken cancellationToken);
}
```

**Contract**:
- `GetLayerRuntimeSettingsAsync` returns null if no runtime state
  exists for the layer (first request — orchestrator initializes)
- `UpsertLayerRuntimeSettingsAsync` creates or updates atomically
- Results MAY be cached in memory by a decorating wrapper

## IVectorTileLayerConfigProvider

Loads and provides access to layer configurations.

```csharp
public interface IVectorTileLayerConfigProvider
{
    VectorTileLayerConfig? GetLayer(int layerId);
    VectorTileLayerConfig? GetLayerByKey(string layerKey);
    IReadOnlyList<VectorTileLayerConfig> GetAllLayers();
    Task ReloadAsync(CancellationToken cancellationToken);
}
```

**Contract**:
- Loaded from JSON files at startup
- `ReloadAsync` re-reads all layer files from disk (called by
  admin reload endpoint)
- Invalid layer files are logged and skipped, not thrown
- Thread-safe: reads can occur concurrently with reload

## Provider Registration

Providers register via DI using a keyed/named pattern:

```csharp
public static class SqlServerProviderExtensions
{
    public static IServiceCollection AddVectorTileHubSqlServerProvider(
        this IServiceCollection services)
    {
        services.AddKeyedSingleton<IVectorTileFeatureProvider,
            SqlServerFeatureProvider>("SqlServer");
        return services;
    }
}
```

The orchestrator resolves the correct provider by matching
`layer.Provider.Type` to the registered key.
