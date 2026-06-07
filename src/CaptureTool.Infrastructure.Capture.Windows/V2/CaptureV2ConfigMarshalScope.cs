using CaptureTool.Domain.Capture.V2;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal sealed class CaptureV2ConfigMarshalScope : IDisposable
{
    private GCHandle _sourcesHandle;
    private GCHandle _audioGainsHandle;
    private GCHandle _outputPathHandle;
    private bool _disposed;

    private CaptureV2ConfigMarshalScope(CaptureV2NativeConfig config)
    {
        Config = config;
    }

    public CaptureV2NativeConfig Config { get; private set; }

    public bool IsDisposed => _disposed;

    public static CaptureV2ConfigMarshalScope FromOptions(CapturePipelineOptions options)
    {
        CapturePipelineOptionsValidator.Validate(options);

        CaptureV2NativeSourceConfig[] sources = options.Sources
            .Select(ToNativeSource)
            .ToArray();
        CaptureV2NativeAudioGainConfig[] audioGains = options.Controls.AudioGains
            .Select(ToNativeAudioGain)
            .ToArray();

        GCHandle sourcesHandle = GCHandle.Alloc(sources, GCHandleType.Pinned);
        GCHandle outputPathHandle = GCHandle.Alloc(options.Output.OutputPath, GCHandleType.Pinned);
        GCHandle audioGainsHandle = default;

        try
        {
            nint audioGainsPointer = 0;
            if (audioGains.Length > 0)
            {
                audioGainsHandle = GCHandle.Alloc(audioGains, GCHandleType.Pinned);
                audioGainsPointer = audioGainsHandle.AddrOfPinnedObject();
            }

            CaptureV2NativeConfig config = new()
            {
                Size = (uint)Marshal.SizeOf<CaptureV2NativeConfig>(),
                Version = CaptureV2NativeMapping.DtoVersion,
                Sources = sourcesHandle.AddrOfPinnedObject(),
                SourceCount = (uint)sources.Length,
                Output = ToNativeOutput(options.Output, outputPathHandle.AddrOfPinnedObject()),
                ToneMapping = ToNativeToneMapping(options.ToneMapping),
                Controls = ToNativeControls(options.Controls, audioGainsPointer, audioGains.Length),
            };

            return new CaptureV2ConfigMarshalScope(config)
            {
                _sourcesHandle = sourcesHandle,
                _outputPathHandle = outputPathHandle,
                _audioGainsHandle = audioGainsHandle,
            };
        }
        catch
        {
            if (audioGainsHandle.IsAllocated)
            {
                audioGainsHandle.Free();
            }

            outputPathHandle.Free();
            sourcesHandle.Free();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_audioGainsHandle.IsAllocated)
        {
            _audioGainsHandle.Free();
        }

        if (_outputPathHandle.IsAllocated)
        {
            _outputPathHandle.Free();
        }

        if (_sourcesHandle.IsAllocated)
        {
            _sourcesHandle.Free();
        }

        Config = default;
        _disposed = true;
    }

    private static CaptureV2NativeSourceConfig ToNativeSource(CaptureSourceOptions source)
        => source switch
        {
            DesktopCaptureSourceOptions desktop => new CaptureV2NativeSourceConfig
            {
                Size = (uint)Marshal.SizeOf<CaptureV2NativeSourceConfig>(),
                Version = CaptureV2NativeMapping.DtoVersion,
                SourceId = desktop.SourceId.Value,
                SourceKind = (int)CaptureV2NativeSourceKind.Desktop,
                CaptureRect = new CaptureV2NativeRect
                {
                    X = desktop.CaptureArea.X,
                    Y = desktop.CaptureArea.Y,
                    Width = desktop.CaptureArea.Width,
                    Height = desktop.CaptureArea.Height,
                },
                PlatformHandle = desktop.MonitorHandle,
                Enabled = ToNativeBool(desktop.Enabled),
            },
            SystemAudioCaptureSourceOptions audio => new CaptureV2NativeSourceConfig
            {
                Size = (uint)Marshal.SizeOf<CaptureV2NativeSourceConfig>(),
                Version = CaptureV2NativeMapping.DtoVersion,
                SourceId = audio.SourceId.Value,
                SourceKind = (int)CaptureV2NativeSourceKind.SystemAudio,
                Enabled = ToNativeBool(audio.Enabled && audio.Armed),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported capture source type."),
        };

    private static CaptureV2NativeOutputConfig ToNativeOutput(CaptureOutputOptions output, nint outputPath)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeOutputConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            OutputPath = outputPath,
            ContainerFormat = CaptureV2NativeMapping.ToNative(output.Container),
            Video = ToNativeVideo(output.Video),
            Audio = ToNativeAudio(output.Audio),
        };

    private static CaptureV2NativeVideoEncodingConfig ToNativeVideo(VideoEncodingOptions? video)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeVideoEncodingConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            Codec = video is null ? (int)CaptureV2NativeVideoCodec.None : CaptureV2NativeMapping.ToNative(video.Codec),
            Bitrate = video?.Bitrate ?? 0,
            FrameRateNumerator = video?.FrameRateNumerator ?? 0,
            FrameRateDenominator = video?.FrameRateDenominator ?? 0,
            GopLength = video?.GopLength ?? 0,
            HardwareAccelerationPreferred = ToNativeBool(video?.HardwareAccelerationPreferred ?? false),
        };

    private static CaptureV2NativeAudioEncodingConfig ToNativeAudio(AudioEncodingOptions? audio)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeAudioEncodingConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            Codec = audio is null ? (int)CaptureV2NativeAudioCodec.None : CaptureV2NativeMapping.ToNative(audio.Codec),
            Bitrate = audio?.Bitrate ?? 0,
            SampleRate = audio?.SampleRate ?? 0,
            Channels = audio?.Channels ?? 0,
        };

    private static CaptureV2NativeToneMappingConfig ToNativeToneMapping(CaptureToneMappingOptions toneMapping)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeToneMappingConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            HdrPolicy = CaptureV2NativeMapping.ToNative(toneMapping.Policy),
            TargetNits = toneMapping.TargetNits,
            PreserveMetadataWhenPossible = ToNativeBool(toneMapping.PreserveMetadataWhenPossible),
        };

    private static CaptureV2NativeControlConfig ToNativeControls(
        CaptureControlOptions controls,
        nint audioGainsPointer,
        int audioGainCount)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeControlConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            StartMuted = ToNativeBool(controls.StartMuted),
            AudioGains = audioGainsPointer,
            AudioGainCount = (uint)audioGainCount,
        };

    private static CaptureV2NativeAudioGainConfig ToNativeAudioGain(CaptureAudioGainOptions gain)
        => new()
        {
            Size = (uint)Marshal.SizeOf<CaptureV2NativeAudioGainConfig>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            SourceId = gain.SourceId.Value,
            GainDb = gain.GainDb,
        };

    private static byte ToNativeBool(bool value) => value ? (byte)1 : (byte)0;
}
