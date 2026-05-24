using System.Security.Claims;

namespace K1Soft.IT.VectorTileHub;

public interface IVectorTileSecurityScopeResolver
{
    Task<VectorTileSecurityScope> ResolveAsync(
        VectorTileLayerConfig layer,
        ClaimsPrincipal user,
        string? scopeOverride,
        CancellationToken cancellationToken);
}
