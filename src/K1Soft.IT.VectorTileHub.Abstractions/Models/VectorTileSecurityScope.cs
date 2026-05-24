using System.Security.Claims;

namespace K1Soft.IT.VectorTileHub;

public sealed class VectorTileSecurityScope
{
    public string ScopeKey { get; init; } = "public";
    public bool IsAuthenticated { get; init; }
    public bool IsAuthorized { get; init; } = true;
    public string[]? FilterValues { get; init; }
    public ClaimsPrincipal Principal { get; init; } = new(new ClaimsIdentity());
}
