namespace K1Soft.IT.VectorTileHub;

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
