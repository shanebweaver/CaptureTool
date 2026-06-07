using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal enum CaptureV2NativeSourceKind
{
    Desktop = 1,
    SystemAudio = 5,
}

internal enum CaptureV2NativeContainerFormat
{
    Mp4 = 1,
}

internal enum CaptureV2NativeVideoCodec
{
    None = 0,
    H264 = 1,
}

internal enum CaptureV2NativeAudioCodec
{
    None = 0,
    Aac = 1,
}

internal enum CaptureV2NativeHdrPolicy
{
    Auto = 0,
    Preserve = 1,
    MapToSdr = 2,
    MatchDisplay = 3,
    ForceSdr = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeSourceConfig
{
    public uint Size;
    public uint Version;
    public uint SourceId;
    public int SourceKind;
    public CaptureV2NativeRect CaptureRect;
    public nint PlatformHandle;
    public byte Enabled;
    public byte Reserved0;
    public ushort Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeVideoEncodingConfig
{
    public uint Size;
    public uint Version;
    public int Codec;
    public uint Bitrate;
    public uint FrameRateNumerator;
    public uint FrameRateDenominator;
    public uint GopLength;
    public byte HardwareAccelerationPreferred;
    public byte Reserved0;
    public ushort Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeAudioEncodingConfig
{
    public uint Size;
    public uint Version;
    public int Codec;
    public uint Bitrate;
    public uint SampleRate;
    public ushort Channels;
    public ushort Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeOutputConfig
{
    public uint Size;
    public uint Version;
    public nint OutputPath;
    public int ContainerFormat;
    public CaptureV2NativeVideoEncodingConfig Video;
    public CaptureV2NativeAudioEncodingConfig Audio;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeToneMappingConfig
{
    public uint Size;
    public uint Version;
    public int HdrPolicy;
    public float TargetNits;
    public byte PreserveMetadataWhenPossible;
    public byte Reserved0;
    public ushort Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeAudioGainConfig
{
    public uint Size;
    public uint Version;
    public uint SourceId;
    public float GainDb;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeControlConfig
{
    public uint Size;
    public uint Version;
    public byte StartMuted;
    public byte Reserved0;
    public ushort Reserved1;
    public nint AudioGains;
    public uint AudioGainCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeConfig
{
    public uint Size;
    public uint Version;
    public nint Sources;
    public uint SourceCount;
    public CaptureV2NativeOutputConfig Output;
    public CaptureV2NativeToneMappingConfig ToneMapping;
    public CaptureV2NativeControlConfig Controls;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeStopResult
{
    public uint Size;
    public uint Version;
    public int ResultCode;
    public int FinalState;
    public int FailureStage;
    public uint Reserved;
    public ulong DroppedVideoFrames;
    public ulong AudioDiscontinuities;
    public ulong LateSamples;
    public ulong UnsupportedCommands;
    public ulong ValidationWarnings;
}
