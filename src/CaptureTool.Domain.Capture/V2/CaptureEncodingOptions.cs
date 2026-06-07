namespace CaptureTool.Domain.Capture.V2;

public enum CaptureContainerFormat
{
    Mp4 = 1,
}

public enum CaptureVideoCodec
{
    H264 = 1,
}

public enum CaptureAudioCodec
{
    Aac = 1,
}

public sealed record VideoEncodingOptions
{
    public static VideoEncodingOptions DefaultH264 { get; } = new()
    {
        Codec = CaptureVideoCodec.H264,
        Bitrate = 8_000_000,
        FrameRateNumerator = 60,
        FrameRateDenominator = 1,
        GopLength = 120,
        HardwareAccelerationPreferred = true,
    };

    public CaptureVideoCodec Codec { get; init; } = CaptureVideoCodec.H264;
    public uint Bitrate { get; init; } = 8_000_000;
    public uint FrameRateNumerator { get; init; } = 60;
    public uint FrameRateDenominator { get; init; } = 1;
    public uint GopLength { get; init; } = 120;
    public bool HardwareAccelerationPreferred { get; init; } = true;
}

public sealed record AudioEncodingOptions
{
    public static AudioEncodingOptions DefaultAac { get; } = new()
    {
        Codec = CaptureAudioCodec.Aac,
        Bitrate = 192_000,
        SampleRate = 48_000,
        Channels = 2,
    };

    public CaptureAudioCodec Codec { get; init; } = CaptureAudioCodec.Aac;
    public uint Bitrate { get; init; } = 192_000;
    public uint SampleRate { get; init; } = 48_000;
    public ushort Channels { get; init; } = 2;
}
