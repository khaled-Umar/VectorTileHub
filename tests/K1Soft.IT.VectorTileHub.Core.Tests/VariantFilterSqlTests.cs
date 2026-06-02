using K1Soft.IT.VectorTileHub;
using Xunit;

namespace K1Soft.IT.VectorTileHub.Core.Tests;

public class VariantFilterSqlTests
{
    private static string Quote(string id) => $"[{id}]";
    private static string Param(int i) => $"@v{i}";

    [Fact]
    public void NullFilter_ReturnsNullPredicate()
    {
        var sql = VariantFilterSql.Build(null, Quote, Param, out var values);
        Assert.Null(sql);
        Assert.Empty(values);
    }

    [Fact]
    public void Equals_SingleValue_EmitsEquality()
    {
        var filter = new ResolvedFilter { Column = "Type_t", Operator = FilterOperator.Equals, Values = ["Villa"] };
        var sql = VariantFilterSql.Build(filter, Quote, Param, out var values);

        Assert.Equal("[Type_t] = @v0", sql);
        Assert.Equal(["Villa"], values);
    }

    [Fact]
    public void In_MultipleValues_EmitsParameterizedInList()
    {
        var filter = new ResolvedFilter { Column = "Type_t", Operator = FilterOperator.In, Values = ["A", "B", "C"] };
        var sql = VariantFilterSql.Build(filter, Quote, Param, out var values);

        Assert.Equal("[Type_t] IN (@v0, @v1, @v2)", sql);
        Assert.Equal(["A", "B", "C"], values);
    }

    [Fact]
    public void IsNull_EmitsIsNull_NoValues()
    {
        var filter = new ResolvedFilter { Column = "Type_t", Operator = FilterOperator.IsNull };
        var sql = VariantFilterSql.Build(filter, Quote, Param, out var values);

        Assert.Equal("[Type_t] IS NULL", sql);
        Assert.Empty(values);
    }
}
