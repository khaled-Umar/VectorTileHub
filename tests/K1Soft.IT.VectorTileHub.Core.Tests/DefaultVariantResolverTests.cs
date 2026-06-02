using K1Soft.IT.VectorTileHub;
using Xunit;

namespace K1Soft.IT.VectorTileHub.Core.Tests;

public class DefaultVariantResolverTests
{
    private static VectorTileLayerConfig LayerWithRules() => new()
    {
        Id = 1,
        LayerKey = "k",
        CacheRules =
        [
            new CacheRuleConfig { VariantKey = "default", IsDefault = true },
            new CacheRuleConfig
            {
                VariantKey = "residential",
                Filter = new FilterConfig { Column = "Type_t", Operator = FilterOperator.In, Values = ["Villa", "Admin"] }
            }
        ]
    };

    [Fact]
    public void NoKey_ResolvesToDefaultVariant()
    {
        var resolver = new DefaultVariantResolver();
        var variant = resolver.Resolve(LayerWithRules(), null);

        Assert.NotNull(variant);
        Assert.Equal("default", variant!.VariantKey);
        Assert.True(variant.IsDefault);
        Assert.Null(variant.Filter);
    }

    [Fact]
    public void KnownKey_ResolvesVariantWithFilter()
    {
        var resolver = new DefaultVariantResolver();
        var variant = resolver.Resolve(LayerWithRules(), "residential");

        Assert.NotNull(variant);
        Assert.Equal("residential", variant!.VariantKey);
        Assert.NotNull(variant.Filter);
        Assert.Equal("Type_t", variant.Filter!.Column);
        Assert.Equal(FilterOperator.In, variant.Filter.Operator);
        Assert.Equal(["Villa", "Admin"], variant.Filter.Values);
    }

    [Fact]
    public void UnknownKey_ReturnsNull()
    {
        var resolver = new DefaultVariantResolver();
        Assert.Null(resolver.Resolve(LayerWithRules(), "does-not-exist"));
    }

    [Fact]
    public void NoRules_DefaultKeyResolves_UnknownKeyIsNull()
    {
        var resolver = new DefaultVariantResolver();
        var layer = new VectorTileLayerConfig { Id = 2, LayerKey = "k" };

        Assert.NotNull(resolver.Resolve(layer, null));
        Assert.NotNull(resolver.Resolve(layer, "default"));
        Assert.Null(resolver.Resolve(layer, "secret"));
    }
}
