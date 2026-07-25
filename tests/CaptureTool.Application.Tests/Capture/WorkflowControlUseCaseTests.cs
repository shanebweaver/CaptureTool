using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Tests.Capture.Audio;
using CaptureTool.Application.Tests.Capture.Video;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class WorkflowControlUseCaseTests
{
    [TestMethod]
    public async Task PrepareVideoCapture_PreparesWorkflow()
    {
        var workflow = new FakeVideoCaptureWorkflow();
        var useCase = new PrepareVideoCaptureUseCase(workflow, TestUseCaseExecutor.Instance);

        var response = await useCase.ExecuteAsync(
            new PrepareVideoCaptureRequest(),
            TestContext.CancellationToken);

        Assert.IsNotNull(response.Value);
        Assert.IsTrue(workflow.PrepareWasCalled);
    }

    [TestMethod]
    public async Task SetVideoCaptureAudioInputMuted_UpdatesWorkflow()
    {
        var workflow = new FakeVideoCaptureWorkflow();
        var useCase = new SetVideoCaptureAudioInputMutedUseCase(
            workflow,
            TestUseCaseExecutor.Instance);

        var response = await useCase.ExecuteAsync(
            new SetVideoCaptureAudioInputMutedRequest(true),
            TestContext.CancellationToken);

        Assert.IsNotNull(response.Value);
        Assert.IsTrue(workflow.IsAudioInputMuted);
        Assert.IsTrue(workflow.LastAudioInputMuted);
    }

    [TestMethod]
    public async Task SelectAudioCaptureInputSource_UpdatesWorkflow()
    {
        var workflow = new FakeAudioCaptureWorkflow();
        var useCase = new SelectAudioCaptureInputSourceUseCase(
            workflow,
            TestUseCaseExecutor.Instance);

        var response = await useCase.ExecuteAsync(
            new SelectAudioCaptureInputSourceRequest("microphone"),
            TestContext.CancellationToken);

        Assert.IsNotNull(response.Value);
        Assert.AreEqual("microphone", workflow.SelectedAudioInputSourceId);
        Assert.AreEqual(1, workflow.SelectAudioInputSourceCallCount);
    }

    public TestContext TestContext { get; set; } = null!;
}
