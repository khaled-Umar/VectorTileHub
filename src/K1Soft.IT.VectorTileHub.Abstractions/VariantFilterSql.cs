using System.Text;

namespace K1Soft.IT.VectorTileHub;

/// <summary>
/// Builds a parameterized SQL predicate for a resolved variant filter. Identifiers
/// are quoted/validated by the provider-supplied <paramref name="quote"/> delegate;
/// values are always emitted as parameter placeholders (never concatenated).
/// </summary>
public static class VariantFilterSql
{
    /// <summary>
    /// Returns the predicate text (without a leading AND) or null when no predicate
    /// applies. <paramref name="values"/> receives the ordered parameter values to bind.
    /// </summary>
    public static string? Build(
        ResolvedFilter? filter,
        Func<string, string> quote,
        Func<int, string> parameterName,
        out string[] values)
    {
        values = [];
        if (filter is null || string.IsNullOrWhiteSpace(filter.Column))
        {
            return null;
        }

        var column = quote(filter.Column);

        switch (filter.Operator)
        {
            case FilterOperator.IsNull:
                return $"{column} IS NULL";

            case FilterOperator.IsNotNull:
                return $"{column} IS NOT NULL";

            case FilterOperator.NotEquals:
                if (filter.Values.Length == 0)
                {
                    return null;
                }

                values = [filter.Values[0]];
                return $"{column} <> {parameterName(0)}";

            case FilterOperator.Equals when filter.Values.Length == 1:
                values = [filter.Values[0]];
                return $"{column} = {parameterName(0)}";

            case FilterOperator.Equals:
            case FilterOperator.In:
                if (filter.Values.Length == 0)
                {
                    return null;
                }

                values = filter.Values;
                var placeholders = new StringBuilder();
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                    {
                        placeholders.Append(", ");
                    }

                    placeholders.Append(parameterName(i));
                }

                return $"{column} IN ({placeholders})";

            default:
                return null;
        }
    }
}
