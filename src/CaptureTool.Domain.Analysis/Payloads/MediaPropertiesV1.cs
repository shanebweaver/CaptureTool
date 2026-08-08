namespace CaptureTool.Domain.Analysis.Payloads;

public readonly record struct PixelSize
{
    public PixelSize(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Pixel width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Pixel height must be positive.");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed class MediaPropertiesV1 : CapabilityPayload
{
    public MediaPropertiesV1(
        CaptureMediaKind mediaKind,
        PixelSize? pixelSize = null,
        TimeSpan? duration = null,
        string? mimeType = null,
        string? container = null,
        string? videoCodec = null,
        string? audioCodec = null,
        int? audioChannelCount = null,
        int? sampleRateHz = null,
        long? bitRate = null,
        double? frameRate = null)
    {
        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (pixelSize is { IsEmpty: true })
        {
            throw new ArgumentException("Pixel dimensions must be positive.", nameof(pixelSize));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
        }

        if (audioChannelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(audioChannelCount), "Audio channel count must be positive.");
        }

        if (sampleRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), "Sample rate must be positive.");
        }

        if (bitRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "Bit rate must be positive.");
        }

        if (frameRate.HasValue && (!double.IsFinite(frameRate.Value) || frameRate.Value <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be finite and positive.");
        }

        MediaKind = mediaKind;
        PixelSize = pixelSize;
        Duration = duration;
        MimeType = PayloadValidation.NormalizeOptional(mimeType, nameof(mimeType));
        Container = PayloadValidation.NormalizeOptional(container, nameof(container));
        VideoCodec = PayloadValidation.NormalizeOptional(videoCodec, nameof(videoCodec));
        AudioCodec = PayloadValidation.NormalizeOptional(audioCodec, nameof(audioCodec));
        AudioChannelCount = audioChannelCount;
        SampleRateHz = sampleRateHz;
        BitRate = bitRate;
        FrameRate = frameRate;
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.MediaPropertiesV1;

    public CaptureMediaKind MediaKind { get; }

    public PixelSize? PixelSize { get; }

    public TimeSpan? Duration { get; }

    public string? MimeType { get; }

    public string? Container { get; }

    public string? VideoCodec { get; }

    public string? AudioCodec { get; }

    public int? AudioChannelCount { get; }

    public int? SampleRateHz { get; }

    public long? BitRate { get; }

    public double? FrameRate { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is MediaPropertiesV1 properties &&
            MediaKind == properties.MediaKind &&
            PixelSize == properties.PixelSize &&
            Duration == properties.Duration &&
            string.Equals(MimeType, properties.MimeType, StringComparison.Ordinal) &&
            string.Equals(Container, properties.Container, StringComparison.Ordinal) &&
            string.Equals(VideoCodec, properties.VideoCodec, StringComparison.Ordinal) &&
            string.Equals(AudioCodec, properties.AudioCodec, StringComparison.Ordinal) &&
            AudioChannelCount == properties.AudioChannelCount &&
            SampleRateHz == properties.SampleRateHz &&
            BitRate == properties.BitRate &&
            FrameRate == properties.FrameRate;
    }
}
