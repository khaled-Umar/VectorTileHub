namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// A resolved, parameterized filter applied server-side by a provider.
/// </summary>
public sealed class ResolvedFilter
{
    public string Column { get; init; } = "";
    public FilterOperator Operator { get; init; } = FilterOperator.Equals;
    public string[] Values { get; init; } = [];
}

/// <summary>
/// The resolved cache variant for a request. Replaces the 001 security scope:
/// it carries no authentication/authorization — only the variant key (used in the
/// cache key) and an optional server-side filter.
/// </summary>
public sealed class VectorTileVariant
{
    public const string DefaultKey = "default";

    public string VariantKey { get; init; } = DefaultKey;
    public ResolvedFilter? Filter { get; init; }
    public bool IsDefault { get; init; }
}
