using System.Collections.Concurrent;

namespace K1Soft.IT.VectorTileHub.Storage;

/// <summary>
/// Singleton in-memory mirror of the global server settings. Reads are served from
/// memory to avoid hitting the database on the hot path; writes go through the
/// durable store and refresh this mirror (write-through).
/// </summary>
public sealed class ServerSettingsMirror
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value) => _values[key] = value;

    public void LoadAll(IEnumerable<KeyValuePair<string, string>> settings)
    {
        foreach (var (key, value) in settings)
        {
            _values[key] = value;
        }
    }
}
