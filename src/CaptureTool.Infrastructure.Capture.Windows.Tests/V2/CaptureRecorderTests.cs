using CaptureTool.Domain.Capture.V2;
using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
public sealed class CaptureRecorderTests
{
    [TestMethod]
    public async Task StartPauseResumeStopAsync_WithValidOptions_Succeeds()
    {
        await using var recorder = new CaptureRecorder();

        await recorder.StartAsync(CreateOptions());
        await recorder.PauseAsync();
        await recorder.ResumeAsync();
        CaptureStopResult stopResult = await recorder.StopAsync();

        stopResult.ResultCode.Should().Be(CaptureV2ResultCode.Success);
    }

    [TestMethod]
    public async Task PauseAsync_BeforeStart_ThrowsNativeException()
    {
        await using var recorder = new CaptureRecorder();

        Func<Task> act = () => recorder.PauseAsync();

        await act.Should().ThrowAsync<CaptureNativeException>()
            .Where(exception => exception.ResultCode == CaptureV2ResultCode.InvalidState)
            .Where(exception => exception.Operation == "Pause");
    }

    [TestMethod]
    public async Task StopAsync_WhenIdle_ReturnsAlreadyStopped()
    {
        await using var recorder = new CaptureRecorder();

        CaptureStopResult result = await recorder.StopAsync();

        result.ResultCode.Should().Be(CaptureV2ResultCode.AlreadyStopped);
    }

    [TestMethod]
    public async Task CancellationBeforeDispatch_PreventsNativeStart()
    {
        await using var recorder = new CaptureRecorder();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> start = () => recorder.StartAsync(CreateOptions(), cancellation.Token);

        await start.Should().ThrowAsync<OperationCanceledException>();
        Func<Task> pause = () => recorder.PauseAsync();
        await pause.Should().ThrowAsync<CaptureNativeException>()
            .Where(exception => exception.ResultCode == CaptureV2ResultCode.InvalidState);
    }

    [TestMethod]
    public async Task ConcurrentAudioCommands_AreSerializedAndComplete()
    {
        await using var recorder = new CaptureRecorder();
        await recorder.StartAsync(CreateOptions());

        Task[] commands =
        [
            recorder.SetAudioGainAsync(new CaptureSourceId(2), -3.0F),
            recorder.SetAudioMutedAsync(new CaptureSourceId(2), true),
            recorder.SetAudioGainAsync(new CaptureSourceId(2), 0.0F),
            recorder.SetAudioMutedAsync(new CaptureSourceId(2), false),
        ];

        Func<Task> act = () => Task.WhenAll(commands);

        await act.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_WithIdleRecorder_ReleasesHandle()
    {
        var recorder = new CaptureRecorder();

        await recorder.DisposeAsync();
        Func<Task> act = () => recorder.PauseAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task DisposeAsync_WithActiveRecorder_StopsBeforeDestroy()
    {
        var recorder = new CaptureRecorder();
        await recorder.StartAsync(CreateOptions());

        await recorder.DisposeAsync();
        await recorder.DisposeAsync();

        Func<Task> act = async () => await recorder.StopAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task SetAudioGainAsync_MissingSource_ThrowsNativeException()
    {
        await using var recorder = new CaptureRecorder();
        await recorder.StartAsync(CreateOptions());

        Func<Task> act = () => recorder.SetAudioGainAsync(new CaptureSourceId(99), 0.0F);

        await act.Should().ThrowAsync<CaptureNativeException>()
            .Where(exception => exception.ResultCode == CaptureV2ResultCode.NotFound)
            .Where(exception => exception.Operation == "SetAudioGain");
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
            Output = new CaptureOutputOptions
            {
                OutputPath = "C:\\Temp\\capture-v2.mp4",
                Container = CaptureContainerFormat.Mp4,
                Video = VideoEncodingOptions.DefaultH264,
                Audio = AudioEncodingOptions.DefaultAac,
            },
            Controls = new CaptureControlOptions
            {
                AudioGains = [CaptureAudioGainOptions.Unity(new CaptureSourceId(2))],
            },
        };
}
