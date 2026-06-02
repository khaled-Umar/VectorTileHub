namespace K1Soft.IT.VectorTileHub.Storage;

public sealed class ServerSettingEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
