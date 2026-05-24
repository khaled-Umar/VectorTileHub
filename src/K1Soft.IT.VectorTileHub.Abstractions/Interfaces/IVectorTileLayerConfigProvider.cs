namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileLayerConfigProvider
{
    VectorTileLayerConfig? GetLayer(int layerId);
    VectorTileLayerConfig? GetLayerByKey(string layerKey);
    IReadOnlyList<VectorTileLayerConfig> GetAllLayers();
    Task ReloadAsync(CancellationToken cancellationToken);
}
