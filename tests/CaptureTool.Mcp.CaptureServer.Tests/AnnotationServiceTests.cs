using CaptureTool.Domain.Edit.Drawable;
using FluentAssertions;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class AnnotationServiceTests
{
    private static readonly DateTimeOffset AnnotationTime = new(2026, 7, 11, 18, 42, 31, TimeSpan.Zero);

    [TestMethod]
    public void CreateArrowWithOptionalLabel_WhenLabelProvided_CreatesTextDrawable()
    {
        var factory = new AnnotationDrawableFactory();

        AnnotationDrawableSet drawables = factory.CreateArrowWithOptionalLabel(new Size(400, 300), 20, 30, 200, 120, "Hello MCP!");

        drawables.Drawables.OfType<ArrowDrawable>().Should().ContainSingle();
        drawables.Drawables.OfType<TextDrawable>().Should().ContainSingle(text => text.Text == "Hello MCP!");
        drawables.Placements.Should().ContainSingle(placement => placement.Kind == "text" && placement.Label == "Hello MCP!");
    }

    [TestMethod]
    public void AnnotateWithArrow_RendersAnnotatedCaptureAndStoresIt()
    {
        var captureStore = new InMemoryMcpCaptureStore();
        McpCapture sourceCapture = CreateStoredCapture("capture:source", new Size(300, 200));
        captureStore.Store(sourceCapture);
        var annotationService = new AnnotationService(
            captureStore,
            new AnnotationDrawableFactory(),
            new ManualTimeProvider(AnnotationTime));

        McpCapture annotated = annotationService.AnnotateWithArrow("capture:source", 20, 30, 220, 150, "Hello MCP!");

        annotated.Metadata.SourceKind.Should().Be("annotatedImage");
        annotated.Metadata.SourceCaptureId.Should().Be("capture:source");
        annotated.Metadata.AnnotationPlacements.Should().ContainSingle(placement => placement.Kind == "text" && placement.Label == "Hello MCP!");
        captureStore.TryGet(annotated.Metadata.CaptureId, out McpCapture storedCapture).Should().BeTrue();
        storedCapture.Should().BeSameAs(annotated);
        annotated.PngBytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
    }

    private static McpCapture CreateStoredCapture(string captureId, Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        var metadata = McpCaptureMetadata.Create(
            captureId,
            AnnotationTime,
            size.Width,
            size.Height,
            dpi: 96,
            scale: 1,
            new Rectangle(Point.Empty, size),
            "region",
            "png");

        return new McpCapture(stream.ToArray(), metadata);
    }
}
