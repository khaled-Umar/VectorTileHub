using System.Security.Claims;

namespace K1Soft.IT.VectorTileHub;

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
