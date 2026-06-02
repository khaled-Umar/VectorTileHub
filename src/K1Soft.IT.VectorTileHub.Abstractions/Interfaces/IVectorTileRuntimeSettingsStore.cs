namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileRuntimeSettingsStore
{
    // Per-layer operational state (durable).
    Task<VectorTileLayerRuntimeSettings?> GetLayerRuntimeSettingsAsync(
        int layerId,
        CancellationToken cancellationToken);

    Task UpsertLayerRuntimeSettingsAsync(
        VectorTileLayerRuntimeSettings settings,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorTileLayerRuntimeSettings>> GetAllAsync(
        CancellationToken cancellationToken);

    // Global key/value settings (e.g. ActiveCacheRootPath), mirrored in memory for
    // fast reads and written through to the durable store on change.
    string? GetSetting(string key);

    Task SetSettingAsync(string key, string value, CancellationToken cancellationToken);
}
