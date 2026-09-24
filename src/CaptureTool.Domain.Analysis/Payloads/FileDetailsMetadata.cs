namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>Original pixel dimensions after display orientation; never analysis thumbnails.</summary>
public sealed record MediaDimensions
{
    public uint Width { get; }
    public uint Height { get; }
    public double AspectRatio => Width / (double)Height;

    public MediaDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
    }
}

public sealed record ImageFileDetails
{
    public MediaDimensions Dimensions { get; }
    public double? DpiX { get; }
    public double? DpiY { get; }

    public ImageFileDetails(MediaDimensions dimensions, double? dpiX = null, double? dpiY = null)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        Dimensions = dimensions;
        DpiX = FileDetailsMetadata.Positive(dpiX, nameof(dpiX));
        DpiY = FileDetailsMetadata.Positive(dpiY, nameof(dpiY));
    }
}

public sealed record VideoFileDetails
{
    public MediaDimensions Dimensions { get; }
    public double? FrameRate { get; }
    public uint? Bitrate { get; }
    public string? Codec { get; }

    public VideoFileDetails(MediaDimensions dimensions, double? frameRate = null, uint? bitrate = null, string? codec = null)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        Dimensions = dimensions;
        FrameRate = FileDetailsMetadata.Positive(frameRate, nameof(frameRate));
        if (bitrate == 0) throw new ArgumentOutOfRangeException(nameof(bitrate));
        Bitrate = bitrate;
        Codec = FileDetailsMetadata.OptionalText(codec, nameof(codec));
    }
}

/// <summary>Audio stream properties; for video, describes its first embedded audio track.</summary>
public sealed record AudioFileDetails
{
    public uint? Channels { get; }
    public uint? SampleRate { get; }
    public uint? Bitrate { get; }
    public string? Codec { get; }

    public AudioFileDetails(uint? channels = null, uint? sampleRate = null, uint? bitrate = null, string? codec = null)
    {
        if (channels == 0 || sampleRate == 0 || bitrate == 0) throw new ArgumentOutOfRangeException(nameof(channels));
        Channels = channels;
        SampleRate = sampleRate;
        Bitrate = bitrate;
        Codec = FileDetailsMetadata.OptionalText(codec, nameof(codec));
    }
}

/// <summary>Local file facts. Missing media properties remain null; capture and filesystem dates are distinct.</summary>
public sealed class FileDetailsMetadata : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.FileDetails;
    public AnalysisMediaKind MediaKind { get; }
    public string FileName { get; }
    public long SizeBytes { get; }
    public string? ContentType { get; }
    public DateTimeOffset FileCreatedAt { get; }
    public DateTimeOffset FileModifiedAt { get; }
    public DateTimeOffset? CapturedAt { get; }
    public TimeSpan? Duration { get; }
    public ImageFileDetails? Image { get; }
    public VideoFileDetails? Video { get; }
    public AudioFileDetails? Audio { get; }

    public FileDetailsMetadata(AnalysisMediaKind mediaKind, string fileName, long sizeBytes, string? contentType,
        DateTimeOffset fileCreatedAt, DateTimeOffset fileModifiedAt, DateTimeOffset? capturedAt = null,
        TimeSpan? duration = null, ImageFileDetails? image = null, VideoFileDetails? video = null, AudioFileDetails? audio = null)
    {
        if (!Enum.IsDefined(mediaKind)) throw new ArgumentOutOfRangeException(nameof(mediaKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Length > 255 || fileName.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("Use a file name without a path.", nameof(fileName));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (image != null && mediaKind != AnalysisMediaKind.Image || video != null && mediaKind != AnalysisMediaKind.Video ||
            mediaKind == AnalysisMediaKind.Image && (audio != null || duration != null))
            throw new ArgumentException("File properties must match the media kind.");
        MediaKind = mediaKind;
        FileName = fileName;
        SizeBytes = sizeBytes;
        ContentType = OptionalText(contentType, nameof(contentType));
        FileCreatedAt = fileCreatedAt.ToUniversalTime();
        FileModifiedAt = fileModifiedAt.ToUniversalTime();
        CapturedAt = capturedAt?.ToUniversalTime();
        Duration = duration;
        Image = image;
        Video = video;
        Audio = audio;
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind == MediaKind;

    internal static double? Positive(double? value, string name) => value is { } number && (!double.IsFinite(number) || number <= 0)
        ? throw new ArgumentOutOfRangeException(name) : value;

    internal static string? OptionalText(string? value, string name) => value != null && (string.IsNullOrWhiteSpace(value) || value.Length > 255)
        ? throw new ArgumentException("Use a bounded nonempty value.", name) : value;
}
