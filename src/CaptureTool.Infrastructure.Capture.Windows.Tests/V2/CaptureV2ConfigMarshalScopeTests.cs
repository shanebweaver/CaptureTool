using CaptureTool.Domain.Capture.V2;
using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
public sealed class CaptureV2ConfigMarshalScopeTests
{
    [TestMethod]
    public void NativeDtoStructSizes_MatchExpectedX64Layout()
    {
        Marshal.SizeOf<CaptureV2NativeSourceConfig>().Should().Be(48);
        Marshal.SizeOf<CaptureV2NativeVideoEncodingConfig>().Should().Be(32);
        Marshal.SizeOf<CaptureV2NativeAudioEncodingConfig>().Should().Be(24);
        Marshal.SizeOf<CaptureV2NativeOutputConfig>().Should().Be(80);
        Marshal.SizeOf<CaptureV2NativeToneMappingConfig>().Should().Be(20);
        Marshal.SizeOf<CaptureV2NativeAudioGainConfig>().Should().Be(20);
        Marshal.SizeOf<CaptureV2NativeControlConfig>().Should().Be(32);
        Marshal.SizeOf<CaptureV2NativeConfig>().Should().Be(168);
        Marshal.SizeOf<CaptureV2NativeStopResult>().Should().Be(64);
    }

    [TestMethod]
    public void FromOptions_MapsConfigHeaderAndOutputPath()
    {
        using CaptureV2ConfigMarshalScope scope = CaptureV2ConfigMarshalScope.FromOptions(CreateOptions());

        CaptureV2NativeConfig config = scope.Config;

        config.Size.Should().Be((uint)Marshal.SizeOf<CaptureV2NativeConfig>());
        config.Version.Should().Be(CaptureV2NativeMapping.DtoVersion);
        config.Output.OutputPath.Should().NotBe(0);
        Marshal.PtrToStringUni(config.Output.OutputPath).Should().Be("C:\\Temp\\capture-v2.mp4");
    }

    [TestMethod]
    public void FromOptions_MapsSourcesDeterministically()
    {
        using CaptureV2ConfigMarshalScope scope = CaptureV2ConfigMarshalScope.FromOptions(CreateOptions());

        CaptureV2NativeSourceConfig desktop = ReadSource(
            scope.Config.Sources,
            index: 0);
        CaptureV2NativeSourceConfig audio = ReadSource(
            scope.Config.Sources,
            index: 1);

        scope.Config.SourceCount.Should().Be(2);
        desktop.SourceId.Should().Be(1);
        desktop.SourceKind.Should().Be((int)CaptureV2NativeSourceKind.Desktop);
        desktop.CaptureRect.Width.Should().Be(1920);
        desktop.PlatformHandle.Should().Be(123);
        desktop.Enabled.Should().Be(1);
        audio.SourceId.Should().Be(2);
        audio.SourceKind.Should().Be((int)CaptureV2NativeSourceKind.SystemAudio);
        audio.Enabled.Should().Be(1);
    }

    [TestMethod]
    public void FromOptions_MapsEncodingToneMappingAndControls()
    {
        using CaptureV2ConfigMarshalScope scope = CaptureV2ConfigMarshalScope.FromOptions(CreateOptions());

        CaptureV2NativeConfig config = scope.Config;
        CaptureV2NativeAudioGainConfig gain = ReadAudioGain(
            config.Controls.AudioGains,
            index: 0);

        config.Output.ContainerFormat.Should().Be((int)CaptureV2NativeContainerFormat.Mp4);
        config.Output.Video.Codec.Should().Be((int)CaptureV2NativeVideoCodec.H264);
        config.Output.Video.Bitrate.Should().Be(8_000_000);
        config.Output.Audio.Codec.Should().Be((int)CaptureV2NativeAudioCodec.Aac);
        config.Output.Audio.SampleRate.Should().Be(48_000);
        config.ToneMapping.HdrPolicy.Should().Be((int)CaptureV2NativeHdrPolicy.Auto);
        config.Controls.StartMuted.Should().Be(1);
        config.Controls.AudioGainCount.Should().Be(1);
        gain.SourceId.Should().Be(2);
        gain.GainDb.Should().Be(0.0F);
    }

    [TestMethod]
    public void FromOptions_MapsMissingOptionalStreamToNone()
    {
        CapturePipelineOptions options = CreateOptions() with
        {
            Output = CreateOutput() with { Audio = null },
            Controls = CaptureControlOptions.Default,
        };

        using CaptureV2ConfigMarshalScope scope = CaptureV2ConfigMarshalScope.FromOptions(options);

        scope.Config.Output.Video.Codec.Should().Be((int)CaptureV2NativeVideoCodec.H264);
        scope.Config.Output.Audio.Codec.Should().Be((int)CaptureV2NativeAudioCodec.None);
        scope.Config.Controls.AudioGains.Should().Be(0);
        scope.Config.Controls.AudioGainCount.Should().Be(0);
    }

    [TestMethod]
    public void Dispose_ReleasesScopeAndClearsPointers()
    {
        CaptureV2ConfigMarshalScope scope = CaptureV2ConfigMarshalScope.FromOptions(CreateOptions());

        scope.Dispose();
        scope.Dispose();

        scope.IsDisposed.Should().BeTrue();
        scope.Config.Sources.Should().Be(0);
        scope.Config.Output.OutputPath.Should().Be(0);
    }

    private static CaptureV2NativeSourceConfig ReadSource(nint arrayPointer, int index)
    {
        nint itemPointer = nint.Add(arrayPointer, Marshal.SizeOf<CaptureV2NativeSourceConfig>() * index);
        return Marshal.PtrToStructure<CaptureV2NativeSourceConfig>(itemPointer);
    }

    private static CaptureV2NativeAudioGainConfig ReadAudioGain(nint arrayPointer, int index)
    {
        nint itemPointer = nint.Add(arrayPointer, Marshal.SizeOf<CaptureV2NativeAudioGainConfig>() * index);
        return Marshal.PtrToStructure<CaptureV2NativeAudioGainConfig>(itemPointer);
    }

    private static CapturePipelineOptions CreateOptions()
        => new()
        {
            Sources =
            [
                new DesktopCaptureSourceOptions
                {
                    SourceId = new CaptureSourceId(1),
                    MonitorHandle = 123,
                    CaptureArea = new Rectangle(0, 0, 1920, 1080),
                },
                new SystemAudioCaptureSourceOptions
                {
                    SourceId = new CaptureSourceId(2),
                },
            ],
            Output = CreateOutput(),
            ToneMapping = CaptureToneMappingOptions.Auto,
            Controls = new CaptureControlOptions
            {
                StartMuted = true,
                AudioGains = [CaptureAudioGainOptions.Unity(new CaptureSourceId(2))],
            },
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
