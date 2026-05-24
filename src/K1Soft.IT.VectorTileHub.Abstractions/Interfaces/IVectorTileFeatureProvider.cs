namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileFeatureProvider
{
    string ProviderType { get; }

    Task<VectorTileFeatureBatch> GetFeaturesAsync(
        VectorTileFeatureQuery query,
        CancellationToken cancellationToken);
}
