# Contract: Library Extension-Point Interfaces

**Branch**: `002-vector-tile-server`
**Namespace**: `K1Soft.IT.VectorTileHub.Abstractions`

These are the stable contracts hosts and providers implement/replace. They
contain **no authentication or authorization** concepts (host-owned in 002).

## IVectorTileService

Primary entry point used by endpoints (and callable directly by a host proxy).

```csharp
public interface IVectorTileService
{
    Task<VectorTileResult> GetTileAsync(
        int layerId, int z, int x, int y,
        string? variantKey = null,
        CancellationToken ct = default);
}
```
- Resolves the layer, resolves the variant (via `IVectorTileVariantResolver`),
  checks cache, serves cached/stale/on-demand, enqueues background refresh on
  stale, and encodes empty tiles outside the zoom range.

## IVectorTileFeatureProvider (per data provider)

```csharp
public interface IVectorTileFeatureProvider
{
    string ProviderType { get; } // "SqlServer" | "Oracle"
    Task<VectorTileFeatureBatch> GetFeaturesAsync(
        VectorTileFeatureQuery query, CancellationToken ct = default);
}
```
- MUST execute **parameterized** spatial queries (ADO.NET, hot path).
- MUST apply `query.Variant.Filter` server-side as a parameterized predicate.
- MUST emit only whitelisted attributes. MUST NOT know about HTTP/cache.

## IVectorTileEncoder

```csharp
public interface IVectorTileEncoder
{
    byte[] Encode(VectorTileFeatureBatch features, VectorTileEncodingContext ctx);
    byte[] EmptyTile(VectorTileEncodingContext ctx);
}
```

## IVectorTileCache

```csharp
public interface IVectorTileCache
{
    Task<CachedTile?> GetAsync(VectorTileCacheKey key, CancellationToken ct = default);
    Task SetAsync(VectorTileCacheKey key, byte[] tileBytes, VectorTileCacheOptions options, CancellationToken ct = default);
    Task RemoveAsync(VectorTileCacheKey key, CancellationToken ct = default);
    Task RemoveVariantAsync(int layerId, string variantKey, string? cacheVersion = null, CancellationToken ct = default);
}

public sealed record CachedTile(byte[] Bytes, DateTimeOffset WrittenAt);
```
- `CachedTile.WrittenAt` drives stale-while-revalidate (compared to
  `RefreshPeriodMinutes`). Disk + optional memory implementations compose via
  `CompositeTileCache`.

## IVectorTileVariantResolver  **[NEW — replaces IVectorTileSecurityScopeResolver]**

```csharp
public interface IVectorTileVariantResolver
{
    // Returns null when variantKey is supplied but not configured ("variant not found").
    VectorTileVariant? Resolve(VectorTileLayerConfig layer, string? variantKey);
}
```
- No `ClaimsPrincipal`, no auth. Maps the requested key to a configured cache
  rule; null/empty key → the layer's default variant.

## IVectorTileLayerConfigProvider

```csharp
public interface IVectorTileLayerConfigProvider
{
    IReadOnlyList<VectorTileLayerConfig> GetLayers();
    VectorTileLayerConfig? GetLayer(int layerId);
    LayerReloadResult Reload(); // explicit; no file watching
}
```

## IVectorTileRuntimeSettingsStore

```csharp
public interface IVectorTileRuntimeSettingsStore
{
    // Global key/value (memory-mirrored, write-through)
    string? GetSetting(string key);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);

    // Per layer+variant operational state
    Task<LayerVariantRuntimeSettings> GetAsync(int layerId, string variantKey, CancellationToken ct = default);
    Task UpdateAsync(LayerVariantRuntimeSettings settings, CancellationToken ct = default);
}
```
- Durable store (SQLite default or host-supplied) is source of truth; reads
  served from the in-memory mirror.

## Job contracts (Hangfire-invoked)

```csharp
public interface ICacheGenerationJob { Task RunAsync(int layerId, string variantKey, int minZoom, int maxZoom, string cacheVersion); }
public interface ICacheInvalidationJob { Task RunAsync(int layerId, string variantKey, double[] bbox, int srid, int minZoom, int maxZoom); }
public interface ICacheSwapBuildJob { Task RunAsync(int layerId, string variantKey, string newVersion); }   // job A
public interface ICacheDeletionJob { Task RunAsync(int layerId, string variantKey, string cacheVersion); }   // job B / delete
```
- Failures mark `CacheGenerationStatus = Failed`, retain written tiles, allow
  retry.

## Dashboard authorization (host-supplied)

```csharp
app.MapVectorTileHubEndpoints();
app.UseVectorTileHubHangfireDashboard(options =>
{
    options.Authorization = new[] { hostProvidedDashboardAuthFilter };
});
```
- The library never ships a built-in dashboard policy.
