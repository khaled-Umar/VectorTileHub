using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace K1Soft.IT.VectorTileHub;

public sealed class DefaultSecurityScopeResolver : IVectorTileSecurityScopeResolver
{
    private readonly VectorTileHubOptions _options;

    public DefaultSecurityScopeResolver(IOptions<VectorTileHubOptions> options)
    {
        _options = options.Value;
    }

    public Task<VectorTileSecurityScope> ResolveAsync(
        VectorTileLayerConfig layer,
        ClaimsPrincipal user,
        string? scopeOverride,
        CancellationToken cancellationToken)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var requireAuth = layer.Security?.RequireAuthentication ?? _options.DefaultAuthenticationRequired;

        if (!requireAuth && layer.Security is null)
        {
            return Task.FromResult(new VectorTileSecurityScope
            {
                ScopeKey = "public",
                IsAuthenticated = isAuthenticated,
                Principal = user
            });
        }

        if (requireAuth && !isAuthenticated)
        {
            return Task.FromResult(new VectorTileSecurityScope
            {
                ScopeKey = "anonymous",
                IsAuthenticated = false,
                IsAuthorized = false,
                Principal = user
            });
        }

        var mappings = layer.Security?.ScopeMappings;
        if (mappings is null || mappings.Count == 0)
        {
            return Task.FromResult(new VectorTileSecurityScope
            {
                ScopeKey = isAuthenticated ? "authenticated" : "public",
                IsAuthenticated = isAuthenticated,
                Principal = user
            });
        }

        foreach (var (role, values) in mappings)
        {
            if (!user.IsInRole(role))
            {
                continue;
            }

            var scopeKey = !string.IsNullOrWhiteSpace(scopeOverride) && values.Contains(scopeOverride, StringComparer.OrdinalIgnoreCase)
                ? scopeOverride
                : role;

            return Task.FromResult(new VectorTileSecurityScope
            {
                ScopeKey = scopeKey,
                IsAuthenticated = isAuthenticated,
                FilterValues = values,
                Principal = user
            });
        }

        return Task.FromResult(new VectorTileSecurityScope
        {
            ScopeKey = "forbidden",
            IsAuthenticated = isAuthenticated,
            IsAuthorized = false,
            Principal = user
        });
    }
}
