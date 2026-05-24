namespace K1Soft.IT.VectorTileHub;

public sealed record VectorTileCacheKey(
    int LayerId,
    int Z,
    int X,
    int Y,
    string ScopeKey,
    string CacheVersion)
{
    public string ToStringKey() => $"{LayerId}:{ScopeKey}:{CacheVersion}:{Z}:{X}:{Y}";

    public string ToDiskPath(string cacheRoot)
    {
        return Path.Combine(
            cacheRoot,
            LayerId.ToString(),
            Sanitize(ScopeKey),
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
