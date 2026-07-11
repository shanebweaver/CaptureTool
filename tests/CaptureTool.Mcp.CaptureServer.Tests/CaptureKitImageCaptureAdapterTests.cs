using CaptureKit.Abstractions;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class CaptureKitImageCaptureAdapterTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void Capture_InvokesCaptureKitAndStoresPngBytes()
    {
        var imageCaptureService = new Mock<IImageCaptureService>();
        imageCaptureService
            .Setup(service => service.Capture(It.IsAny<ImageCaptureRequest>()))
            .Returns<ImageCaptureRequest>(request =>
            {
                WritePng(request.OutputPath, new Size(4, 3));
                return new ImageCaptureResult(request.OutputPath, 4, 3);
            });
        var captureStore = new InMemoryMcpCaptureStore();
        var adapter = new CaptureKitImageCaptureAdapter(imageCaptureService.Object, captureStore, new ManualTimeProvider(CaptureTime));

        McpCapture capture = adapter.Capture(
            CaptureTarget.Window(123),
            new Rectangle(10, 20, 400, 300),
            "window",
            96,
            1,
            targetId: "hwnd:123",
            targetTitle: "Test Window");

        capture.PngBytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
        capture.Metadata.SourceKind.Should().Be("window");
        capture.Metadata.TargetId.Should().Be("hwnd:123");
        capture.Metadata.TargetTitle.Should().Be("Test Window");
        capture.Metadata.Width.Should().Be(4);
        capture.Metadata.Height.Should().Be(3);
        captureStore.TryGet(capture.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(capture);
    }

    private static void WritePng(string path, Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        bitmap.Save(path, ImageFormat.Png);
    }
}
