using System.Drawing;

namespace CaptureTool.Domain.Capture.V2;

public static class CapturePipelineOptionsValidator
{
    public const float MinAudioGainDb = -60.0F;
    public const float MaxAudioGainDb = 12.0F;

    public static void Validate(CapturePipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Sources);
        ArgumentNullException.ThrowIfNull(options.Output);
        ArgumentNullException.ThrowIfNull(options.Controls);
        ArgumentNullException.ThrowIfNull(options.ToneMapping);

        if (options.Sources.Count == 0)
        {
            throw new ArgumentException("At least one capture source is required.", nameof(options));
        }

        ValidateOutput(options.Output);
        ValidateToneMapping(options.ToneMapping);
        ValidateSources(options.Sources);
        ValidateControls(options.Controls);
    }

    private static void ValidateOutput(CaptureOutputOptions output)
    {
        if (string.IsNullOrWhiteSpace(output.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(output));
        }

        if (!Enum.IsDefined(output.Container))
        {
            throw new ArgumentOutOfRangeException(nameof(output), output.Container, "Container format is unsupported.");
        }

        if (output.Video is null && output.Audio is null)
        {
            throw new ArgumentException("At least one output stream is required.", nameof(output));
        }

        if (output.Video is not null)
        {
            ValidateVideo(output.Video);
        }

        if (output.Audio is not null)
        {
            ValidateAudio(output.Audio);
        }
    }

    private static void ValidateVideo(VideoEncodingOptions video)
    {
        if (!Enum.IsDefined(video.Codec))
        {
            throw new ArgumentOutOfRangeException(nameof(video), video.Codec, "Video codec is unsupported.");
        }

        if (video.Bitrate == 0 || video.FrameRateNumerator == 0 || video.FrameRateDenominator == 0)
        {
            throw new ArgumentException("Video bitrate and frame rate must be positive.", nameof(video));
        }
    }

    private static void ValidateAudio(AudioEncodingOptions audio)
    {
        if (!Enum.IsDefined(audio.Codec))
        {
            throw new ArgumentOutOfRangeException(nameof(audio), audio.Codec, "Audio codec is unsupported.");
        }

        if (audio.Bitrate == 0 || audio.SampleRate == 0 || audio.Channels == 0)
        {
            throw new ArgumentException("Audio bitrate, sample rate, and channel count must be positive.", nameof(audio));
        }
    }

    private static void ValidateToneMapping(CaptureToneMappingOptions toneMapping)
    {
        if (!Enum.IsDefined(toneMapping.Policy))
        {
            throw new ArgumentOutOfRangeException(nameof(toneMapping), toneMapping.Policy, "HDR policy is unsupported.");
        }
    }

    private static void ValidateSources(IReadOnlyList<CaptureSourceOptions> sources)
    {
        HashSet<CaptureSourceId> sourceIds = [];
        foreach (CaptureSourceOptions source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!source.SourceId.IsValid)
            {
                throw new ArgumentException("Source id must be non-zero.", nameof(sources));
            }

            if (!sourceIds.Add(source.SourceId))
            {
                throw new ArgumentException("Source ids must be unique.", nameof(sources));
            }

            if (source is DesktopCaptureSourceOptions desktop)
            {
                ValidateDesktopSource(desktop);
            }
        }
    }

    private static void ValidateDesktopSource(DesktopCaptureSourceOptions desktop)
    {
        if (desktop.Enabled && !IsValidCaptureArea(desktop.CaptureArea))
        {
            throw new ArgumentException(
                "Desktop capture area must be empty for full-monitor capture or have positive width and height.",
                nameof(desktop));
        }
    }

    private static bool IsValidCaptureArea(Rectangle rectangle)
        => rectangle == Rectangle.Empty || (rectangle.Width > 0 && rectangle.Height > 0);

    private static void ValidateControls(CaptureControlOptions controls)
    {
        ArgumentNullException.ThrowIfNull(controls.AudioGains);

        foreach (CaptureAudioGainOptions gain in controls.AudioGains)
        {
            ArgumentNullException.ThrowIfNull(gain);

            if (!gain.SourceId.IsValid)
            {
                throw new ArgumentException("Audio gain source id must be non-zero.", nameof(controls));
            }

            if (gain.GainDb < MinAudioGainDb || gain.GainDb > MaxAudioGainDb)
            {
                throw new ArgumentOutOfRangeException(nameof(controls), gain.GainDb, "Audio gain is outside the supported range.");
            }
        }
    }
}
