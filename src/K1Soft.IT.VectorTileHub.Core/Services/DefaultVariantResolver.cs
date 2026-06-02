namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Default variant resolver. Maps a caller-supplied variant key to one of the
/// layer's <see cref="VectorTileLayerConfig.CacheRules"/>. Performs no auth.
/// </summary>
public sealed class DefaultVariantResolver : IVectorTileVariantResolver
{
    public VectorTileVariant? Resolve(VectorTileLayerConfig layer, string? variantKey)
    {
        var hasKey = !string.IsNullOrWhiteSpace(variantKey);

        // No cache rules configured: only the implicit unfiltered "default" variant exists.
        if (layer.CacheRules.Count == 0)
        {
            if (!hasKey || string.Equals(variantKey, VectorTileVariant.DefaultKey, StringComparison.OrdinalIgnoreCase))
            {
                return new VectorTileVariant { VariantKey = VectorTileVariant.DefaultKey, IsDefault = true };
            }

            return null; // unknown variant
        }

        CacheRuleConfig? rule;
        if (hasKey)
        {
            rule = layer.CacheRules.FirstOrDefault(r =>
                string.Equals(r.VariantKey, variantKey, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
            {
                return null; // "variant not found"
            }
        }
        else
        {
            rule = layer.CacheRules.FirstOrDefault(r => r.IsDefault) ?? layer.CacheRules[0];
        }

        return new VectorTileVariant
        {
            VariantKey = rule.VariantKey,
            IsDefault = rule.IsDefault,
            Filter = rule.Filter is null
                ? null
                : new ResolvedFilter
                {
                    Column = rule.Filter.Column,
                    Operator = rule.Filter.Operator,
                    Values = rule.Filter.Values
                }
        };
    }
}
