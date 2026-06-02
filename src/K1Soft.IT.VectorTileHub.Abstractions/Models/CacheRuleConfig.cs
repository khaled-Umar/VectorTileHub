namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Server-side filter operators usable by a cache variant. Values are always
/// applied as parameterized predicates — never concatenated into SQL.
/// </summary>
public enum FilterOperator
{
    Equals,
    In,
    NotEquals,
    IsNull,
    IsNotNull
}

/// <summary>
/// A trusted, server-side filter definition for a cache variant.
/// </summary>
public sealed class FilterConfig
{
    public string Column { get; set; } = "";
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;
    public string[] Values { get; set; } = [];
}

/// <summary>
/// Defines one filtered cache variant for a layer. The library is role-agnostic:
/// the host maps a user's role to a <see cref="VariantKey"/> and passes it on the
/// tile request. The variant's <see cref="Filter"/> scopes the source rows.
/// </summary>
public sealed class CacheRuleConfig
{
    public string VariantKey { get; set; } = "default";
    public bool IsDefault { get; set; }
    public FilterConfig? Filter { get; set; }
    public string? DisplayName { get; set; }
}
