using System.Drawing;

namespace CaptureTool.Domain.Capture.V2;

public sealed record CapturePipelineOptions
{
    public required IReadOnlyList<CaptureSourceOptions> Sources { get; init; }
    public required CaptureOutputOptions Output { get; init; }
    public CaptureToneMappingOptions ToneMapping { get; init; } = CaptureToneMappingOptions.Auto;
    public CaptureControlOptions Controls { get; init; } = CaptureControlOptions.Default;
}

public abstract record CaptureSourceOptions
{
    public required CaptureSourceId SourceId { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed record DesktopCaptureSourceOptions : CaptureSourceOptions
{
    public nint MonitorHandle { get; init; }
    public Rectangle CaptureArea { get; init; }
}

public sealed record SystemAudioCaptureSourceOptions : CaptureSourceOptions
{
    public bool Armed { get; init; } = true;
}

public sealed record CaptureOutputOptions
{
    public required string OutputPath { get; init; }
    public required CaptureContainerFormat Container { get; init; }
    public VideoEncodingOptions? Video { get; init; }
    public AudioEncodingOptions? Audio { get; init; }
}

public enum CaptureHdrPolicy
{
    Auto = 0,
    Preserve = 1,
    MapToSdr = 2,
    MatchDisplay = 3,
    ForceSdr = 4,
}

public sealed record CaptureToneMappingOptions
{
    public static CaptureToneMappingOptions Auto { get; } = new()
    {
        Policy = CaptureHdrPolicy.Auto,
        TargetNits = 0.0F,
        PreserveMetadataWhenPossible = false,
    };

    public CaptureHdrPolicy Policy { get; init; } = CaptureHdrPolicy.Auto;
    public float TargetNits { get; init; }
    public bool PreserveMetadataWhenPossible { get; init; }
}

public sealed record CaptureControlOptions
{
    public static CaptureControlOptions Default { get; } = new()
    {
        StartMuted = false,
        AudioGains = [],
    };

    public bool StartMuted { get; init; }
    public IReadOnlyList<CaptureAudioGainOptions> AudioGains { get; init; } = [];
}

public sealed record CaptureAudioGainOptions
{
    public static CaptureAudioGainOptions Unity(CaptureSourceId sourceId) => new()
    {
        SourceId = sourceId,
        GainDb = 0.0F,
    };

    public required CaptureSourceId SourceId { get; init; }
    public float GainDb { get; init; }
}
