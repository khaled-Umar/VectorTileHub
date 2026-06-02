namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileService
{
    /// <summary>
    /// Produces an MVT/PBF tile. The optional <paramref name="variantKey"/> selects a
    /// filtered cache variant; the host maps user role → variant key before calling.
    /// The library performs no authentication/authorization.
    /// </summary>
    Task<VectorTileResult> GetTileAsync(
        int layerId,
        int z,
        int x,
        int y,
        string? variantKey,
        CancellationToken cancellationToken);
}
