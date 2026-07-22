using CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Capture.Image.CaptureImage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class ImageCaptureUseCaseTests
{
    [TestMethod]
    public async Task CaptureImageUseCase_ShouldDelegateToWorkflow()
    {
        var workflow = new FakeImageCaptureWorkflow();
        var useCase = new CaptureImageUseCase(workflow, TestUseCaseExecutor.Instance);
        NewCaptureArgs args = CreateCaptureArgs();

        CaptureImageResponse response = (await useCase.ExecuteAsync(
            new CaptureImageRequest(args),
            TestContext.CancellationToken)).Value!;

        Assert.AreSame(workflow.ImageFile, response.Image);
        Assert.AreEqual(args, workflow.CapturedImageArgs);
    }

    [TestMethod]
    public async Task CaptureAllScreensImageUseCase_ShouldDelegateToWorkflow()
    {
        var workflow = new FakeImageCaptureWorkflow();
        var useCase = new CaptureAllScreensImageUseCase(workflow, TestUseCaseExecutor.Instance);
        MonitorCaptureResult[] monitors = [CreateMonitor()];

        CaptureAllScreensImageResponse response = (await useCase.ExecuteAsync(
            new CaptureAllScreensImageRequest(monitors),
            TestContext.CancellationToken)).Value!;

        Assert.AreSame(workflow.ImageFile, response.Image);
        CollectionAssert.AreEqual(monitors, workflow.CapturedMonitors?.ToArray());
    }

    private static NewCaptureArgs CreateCaptureArgs() =>
        new(CreateMonitor(), new Rectangle(1, 2, 3, 4));

    private static MonitorCaptureResult CreateMonitor() =>
        new(1, [], 96, new Rectangle(0, 0, 10, 10), new Rectangle(0, 0, 10, 10), true);

    public TestContext TestContext { get; set; } = null!;

    private sealed class FakeImageCaptureWorkflow : IImageCaptureWorkflow
    {
        public event EventHandler<ImageFile>? NewImageCaptured;

        public ImageFile ImageFile { get; } = new("capture.png");
        public NewCaptureArgs? CapturedImageArgs { get; private set; }
        public IReadOnlyList<MonitorCaptureResult>? CapturedMonitors { get; private set; }

        public ImageFile CaptureAllScreens()
        {
            NewImageCaptured?.Invoke(this, ImageFile);
            return ImageFile;
        }

        public ImageFile CaptureMonitors(IReadOnlyList<MonitorCaptureResult> monitors)
        {
            CapturedMonitors = monitors;
            NewImageCaptured?.Invoke(this, ImageFile);
            return ImageFile;
        }

        public ImageFile CaptureImage(NewCaptureArgs args)
        {
            CapturedImageArgs = args;
            NewImageCaptured?.Invoke(this, ImageFile);
            return ImageFile;
        }
    }
}
