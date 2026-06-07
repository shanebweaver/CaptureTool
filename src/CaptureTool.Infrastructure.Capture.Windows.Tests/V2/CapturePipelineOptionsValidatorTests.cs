using CaptureTool.Domain.Capture.V2;
using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
public sealed class CapturePipelineOptionsValidatorTests
{
    [TestMethod]
    public void Validate_ValidDesktopMp4Options_Passes()
    {
        CapturePipelineOptions options = CreateValidOptions();

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Validate_DuplicateSourceIds_ThrowsArgumentException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Sources =
            [
                CreateDesktopSource(1),
                new SystemAudioCaptureSourceOptions { SourceId = new CaptureSourceId(1) },
            ],
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*unique*");
    }

    [TestMethod]
    public void Validate_FullMonitorDesktopCaptureRectangle_Passes()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Sources =
            [
                CreateDesktopSource(1) with { CaptureArea = Rectangle.Empty },
            ],
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Validate_InvalidDesktopCaptureRectangle_ThrowsArgumentException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Sources =
            [
                CreateDesktopSource(1) with { CaptureArea = new Rectangle(0, 0, 0, 1080) },
            ],
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*full-monitor capture or have positive width and height*");
    }

    [TestMethod]
    public void Validate_MissingOutputPath_ThrowsArgumentException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Output = CreateOutput() with { OutputPath = "" },
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Output path*");
    }

    [TestMethod]
    public void Validate_MissingOutputStreams_ThrowsArgumentException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Output = CreateOutput() with { Video = null, Audio = null },
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*output stream*");
    }

    [TestMethod]
    public void Validate_InvalidEnumValue_ThrowsArgumentOutOfRangeException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Output = CreateOutput() with { Container = (CaptureContainerFormat)999 },
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Validate_AudioGainOutsideRange_ThrowsArgumentOutOfRangeException()
    {
        CapturePipelineOptions options = CreateValidOptions() with
        {
            Controls = new CaptureControlOptions
            {
                AudioGains =
                [
                    new CaptureAudioGainOptions
                    {
                        SourceId = new CaptureSourceId(2),
                        GainDb = CapturePipelineOptionsValidator.MaxAudioGainDb + 1.0F,
                    },
                ],
            },
        };

        Action act = () => CapturePipelineOptionsValidator.Validate(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Defaults_AreExplicitForFirstSlice()
    {
        VideoEncodingOptions.DefaultH264.Codec.Should().Be(CaptureVideoCodec.H264);
        VideoEncodingOptions.DefaultH264.Bitrate.Should().Be(8_000_000);
        AudioEncodingOptions.DefaultAac.Codec.Should().Be(CaptureAudioCodec.Aac);
        AudioEncodingOptions.DefaultAac.SampleRate.Should().Be(48_000);
        CaptureToneMappingOptions.Auto.Policy.Should().Be(CaptureHdrPolicy.Auto);
        CaptureControlOptions.Default.StartMuted.Should().BeFalse();
        CaptureAudioGainOptions.Unity(new CaptureSourceId(2)).GainDb.Should().Be(0.0F);
    }

    [TestMethod]
    public void OptionsModel_DoesNotExposeNativeDtoLayoutFields()
    {
        string[] propertyNames = typeof(CapturePipelineOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        propertyNames.Should().NotContain(["Size", "Version", "Reserved", "PlatformHandle"]);
    }

    private static CapturePipelineOptions CreateValidOptions()
        => new()
        {
            Sources =
            [
                CreateDesktopSource(1),
                new SystemAudioCaptureSourceOptions { SourceId = new CaptureSourceId(2) },
            ],
            Output = CreateOutput(),
            Controls = new CaptureControlOptions
            {
                StartMuted = false,
                AudioGains = [CaptureAudioGainOptions.Unity(new CaptureSourceId(2))],
            },
        };

    private static DesktopCaptureSourceOptions CreateDesktopSource(uint sourceId)
        => new()
        {
            SourceId = new CaptureSourceId(sourceId),
            MonitorHandle = 123,
            CaptureArea = new Rectangle(0, 0, 1920, 1080),
        };

    private static CaptureOutputOptions CreateOutput()
        => new()
        {
            OutputPath = "C:\\Temp\\capture-v2.mp4",
            Container = CaptureContainerFormat.Mp4,
            Video = VideoEncodingOptions.DefaultH264,
            Audio = AudioEncodingOptions.DefaultAac,
        };
}
