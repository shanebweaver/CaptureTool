namespace CaptureTool.Domain.Capture;

public enum CaptureSourceOwnership
{
    Application,
    External,
}

/// <summary>A captured asset outlives recent history and any separately saved copy.</summary>
public sealed record CaptureAsset
{
    public CaptureId Id { get; }
    public CaptureFileType MediaType { get; }
    /// <summary>The known capture time; historical activity/file dates are not substitutes.</summary>
    public DateTimeOffset? CapturedAt { get; }
    public string SourcePath { get; }
    public string? PreferredPath { get; }
    public CaptureSourceOwnership SourceOwnership { get; }
    public CaptureName? Name { get; }

    public CaptureAsset(CaptureId id, CaptureFileType mediaType, DateTimeOffset? capturedAt,
        string sourcePath, CaptureSourceOwnership sourceOwnership, string? preferredPath = null, CaptureName? name = null)
    {
        if (id.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(id));
        if (mediaType is not (CaptureFileType.Image or CaptureFileType.Audio or CaptureFileType.Video))
            throw new ArgumentOutOfRangeException(nameof(mediaType));
        if (!Enum.IsDefined(sourceOwnership)) throw new ArgumentOutOfRangeException(nameof(sourceOwnership));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (preferredPath != null) ArgumentException.ThrowIfNullOrWhiteSpace(preferredPath);

        Id = id;
        MediaType = mediaType;
        CapturedAt = capturedAt;
        SourcePath = sourcePath;
        PreferredPath = preferredPath;
        SourceOwnership = sourceOwnership;
        Name = name;
    }

    public CaptureAsset WithPreferredPath(string? path) =>
        new(Id, MediaType, CapturedAt, SourcePath, SourceOwnership, path, Name);

    public CaptureAsset RelocateSource(string path) =>
        new(Id, MediaType, CapturedAt, path, SourceOwnership, PreferredPath, Name);

    public CaptureAsset WithName(CaptureName name) => new(Id, MediaType, CapturedAt, SourcePath, SourceOwnership, PreferredPath, name);
}
