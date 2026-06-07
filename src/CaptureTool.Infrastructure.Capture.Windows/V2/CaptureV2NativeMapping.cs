using CaptureTool.Domain.Capture.V2;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal static class CaptureV2NativeMapping
{
    public const uint DtoVersion = 1;

    public static int ToNative(CaptureContainerFormat value)
        => value switch
        {
            CaptureContainerFormat.Mp4 => (int)CaptureV2NativeContainerFormat.Mp4,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported container format."),
        };

    public static int ToNative(CaptureVideoCodec value)
        => value switch
        {
            CaptureVideoCodec.H264 => (int)CaptureV2NativeVideoCodec.H264,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported video codec."),
        };

    public static int ToNative(CaptureAudioCodec value)
        => value switch
        {
            CaptureAudioCodec.Aac => (int)CaptureV2NativeAudioCodec.Aac,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported audio codec."),
        };

    public static int ToNative(CaptureHdrPolicy value)
        => value switch
        {
            CaptureHdrPolicy.Auto => (int)CaptureV2NativeHdrPolicy.Auto,
            CaptureHdrPolicy.Preserve => (int)CaptureV2NativeHdrPolicy.Preserve,
            CaptureHdrPolicy.MapToSdr => (int)CaptureV2NativeHdrPolicy.MapToSdr,
            CaptureHdrPolicy.MatchDisplay => (int)CaptureV2NativeHdrPolicy.MatchDisplay,
            CaptureHdrPolicy.ForceSdr => (int)CaptureV2NativeHdrPolicy.ForceSdr,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported HDR policy."),
        };
}
