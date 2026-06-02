namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Maps a caller-supplied variant key to one of a layer's configured cache rules.
/// Performs NO authentication or authorization — the host resolves a user's role
/// to a variant key before calling.
/// </summary>
public interface IVectorTileVariantResolver
{
    /// <summary>
    /// Resolves the variant. Returns null when a non-empty <paramref name="variantKey"/>
    /// is supplied but does not match any configured variant ("variant not found").
    /// A null/empty key resolves to the layer's default variant.
    /// </summary>
    VectorTileVariant? Resolve(VectorTileLayerConfig layer, string? variantKey);
}
