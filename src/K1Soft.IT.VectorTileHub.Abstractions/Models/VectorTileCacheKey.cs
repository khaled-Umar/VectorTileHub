namespace K1Soft.IT.VectorTileHub;

public sealed record VectorTileCacheKey(
    int LayerId,
    int Z,
    int X,
    int Y,
    string VariantKey,
    string CacheVersion)
{
    public string ToStringKey() => $"{LayerId}:{VariantKey}:{CacheVersion}:{Z}:{X}:{Y}";

    public string ToDiskPath(string cacheRoot)
    {
        return Path.Combine(
            cacheRoot,
            LayerId.ToString(),
            Sanitize(VariantKey),
            Sanitize(CacheVersion),
            Z.ToString(),
            X.ToString(),
            $"{Y}.pbf");
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }
}
