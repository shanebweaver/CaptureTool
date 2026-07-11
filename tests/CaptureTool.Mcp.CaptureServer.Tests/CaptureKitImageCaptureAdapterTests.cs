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

    [TestMethod]
    public void Capture_WhenWindowCaptureHasBlankScaledPadding_TrimsPadding()
    {
        var imageCaptureService = new Mock<IImageCaptureService>();
        imageCaptureService
            .Setup(service => service.Capture(It.IsAny<ImageCaptureRequest>()))
            .Returns<ImageCaptureRequest>(request =>
            {
                WritePaddedWindowPng(request.OutputPath);
                return new ImageCaptureResult(request.OutputPath, 5, 4);
            });
        var captureStore = new InMemoryMcpCaptureStore();
        var adapter = new CaptureKitImageCaptureAdapter(imageCaptureService.Object, captureStore, new ManualTimeProvider(CaptureTime));

        McpCapture capture = adapter.Capture(
            CaptureTarget.Window(123),
            new Rectangle(10, 20, 3, 2),
            "window",
            120,
            1.25f,
            targetId: "hwnd:123",
            targetTitle: "Scaled Window");

        capture.Metadata.Width.Should().Be(3);
        capture.Metadata.Height.Should().Be(2);
        using var stream = new MemoryStream(capture.PngBytes);
        using var image = Image.FromStream(stream);
        image.Width.Should().Be(3);
        image.Height.Should().Be(2);
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

    private static void WritePaddedWindowPng(string path)
    {
        using var bitmap = new Bitmap(5, 4);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, 3, 2));
        bitmap.Save(path, ImageFormat.Png);
    }
}
