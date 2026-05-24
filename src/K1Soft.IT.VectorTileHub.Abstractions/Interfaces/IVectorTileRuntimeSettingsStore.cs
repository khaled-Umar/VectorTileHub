namespace K1Soft.IT.VectorTileHub;

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
