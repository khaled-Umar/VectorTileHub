namespace K1Soft.IT.VectorTileHub;

public enum VectorTileResultStatus
{
    Ok,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    ServiceUnavailable
}

public sealed class VectorTileResult
{
    public byte[] TileBytes { get; init; } = [];
    public bool IsEmpty { get; init; }
    public bool FromCache { get; init; }

    /// <summary>True when a stale cached tile was served and a background refresh was enqueued.</summary>
    public bool IsStale { get; init; }

    public string ContentType { get; init; } = "application/x-protobuf";
    public VectorTileResultStatus Status { get; init; } = VectorTileResultStatus.Ok;
    public string? Error { get; init; }

    public static VectorTileResult Failure(VectorTileResultStatus status, string error) => new()
    {
        Status = status,
        Error = error
    };
}
