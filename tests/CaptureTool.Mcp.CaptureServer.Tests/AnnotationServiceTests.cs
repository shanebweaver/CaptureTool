using CaptureTool.Domain.Edit.Drawable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

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
    [DataRow(1, 1)]
    [DataRow(10, 5)]
    [DataRow(79, 35)]
    [DataRow(400, 300)]
    public void CreateArrowWithOptionalLabel_ClampsExtremeCoordinatesAndFitsLabel(int width, int height)
    {
        var factory = new AnnotationDrawableFactory();

        AnnotationDrawableSet drawables = factory.CreateArrowWithOptionalLabel(
            new Size(width, height), int.MinValue, int.MaxValue, int.MaxValue, int.MinValue, " Label ");

        ArrowDrawable arrow = drawables.Drawables.OfType<ArrowDrawable>().Single();
        arrow.Offset.Should().Be(new Vector2(0, height - 1));
        arrow.EndPoint.Should().Be(new Vector2(width - 1, 0));
        Rectangle bounds = drawables.Placements.Single().Bounds.ToRectangle();
        bounds.Width.Should().BePositive();
        bounds.Height.Should().BePositive();
        new Rectangle(0, 0, width, height).Contains(bounds).Should().BeTrue();
        drawables.Placements.Single().Label.Should().Be("Label");
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    [DataRow(-1, 10)]
    public void CreateArrowWithOptionalLabel_RejectsInvalidImageSize(int width, int height)
    {
        var factory = new AnnotationDrawableFactory();

        Action create = () => factory.CreateArrowWithOptionalLabel(new Size(width, height), 0, 0, 1, 1, null);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void CreateArrowWithOptionalLabel_EnforcesLabelLengthBeforeTrimming()
    {
        var factory = new AnnotationDrawableFactory();
        string label = new('a', AnnotationDrawableFactory.MaxLabelLength);

        factory.CreateArrowWithOptionalLabel(new Size(100, 100), 0, 0, 10, 10, label)
            .Placements.Single().Label.Should().Be(label);
        Action tooLong = () => factory.CreateArrowWithOptionalLabel(new Size(100, 100), 0, 0, 10, 10, label + "a");
        Action whitespace = () => factory.CreateArrowWithOptionalLabel(new Size(100, 100), 0, 0, 10, 10, new string(' ', label.Length + 1));

        tooLong.Should().Throw<ArgumentException>().WithMessage("*at most 1024 characters*");
        whitespace.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(10, 5)]
    public void AnnotateWithArrow_OnTinyImage_RendersWithinImageBounds(int width, int height)
    {
        var store = new InMemoryMcpCaptureStore();
        store.Store(CreateStoredCapture("tiny", new Size(width, height)));
        var service = new AnnotationService(store, new AnnotationDrawableFactory(), new ManualTimeProvider(AnnotationTime));

        McpCapture result = service.AnnotateWithArrow("tiny", int.MinValue, int.MaxValue, int.MaxValue, int.MinValue, "Tiny");

        using var stream = new MemoryStream(result.PngBytes);
        using var image = new Bitmap(stream);
        image.Size.Should().Be(new Size(width, height));
        new Rectangle(Point.Empty, image.Size).Contains(result.Metadata.AnnotationPlacements!.Single().Bounds.ToRectangle()).Should().BeTrue();
    }

    [TestMethod]
    public void AnnotateImage_WhenLabelIsTooLong_ReturnsRecoverableErrorBeforeDecodingImage()
    {
        var store = new InMemoryMcpCaptureStore();
        McpCapture source = CreateStoredCapture("source", new Size(10, 10)) with { PngBytes = [0] };
        store.Store(source);
        var service = new AnnotationService(store, new AnnotationDrawableFactory(), new ManualTimeProvider(AnnotationTime));

        var result = AnnotationTool.AnnotateImage(service, NullLogger<AnnotationTool>.Instance,
            "source", 0, 0, 1, 1, new string('a', AnnotationDrawableFactory.MaxLabelLength + 1));

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>()
            .Which.Text.Should().Contain("Shorten the label and retry");
    }

    [TestMethod]
    public void AnnotateImage_WhenSourceWasEvicted_ExplainsHowToRecover()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptures: 1);
        store.Store(CreateStoredCapture("old", new Size(10, 10)));
        store.Store(CreateStoredCapture("new", new Size(10, 10)));
        var service = new AnnotationService(store, new AnnotationDrawableFactory(), new ManualTimeProvider(AnnotationTime));

        var result = AnnotationTool.AnnotateImage(service, NullLogger<AnnotationTool>.Instance, "old", 0, 0, 1, 1);

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>()
            .Which.Text.Should().Contain("evicted").And.Contain("Capture the image again and retry");
    }

    [TestMethod]
    public void AnnotateImage_WhenResultExceedsStorageLimit_ReturnsRecoverableErrorAndPreservesSource()
    {
        McpCapture source = CreateStoredCapture("source", new Size(300, 200));
        var store = new InMemoryMcpCaptureStore(maxCaptureBytes: source.PngBytes.Length);
        store.Store(source);
        var service = new AnnotationService(store, new AnnotationDrawableFactory(), new ManualTimeProvider(AnnotationTime));

        var result = AnnotationTool.AnnotateImage(service, NullLogger<AnnotationTool>.Instance,
            "source", 20, 30, 220, 150, "Label");

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>()
            .Which.Text.Should().Contain("storage limit").And.Contain("Capture a smaller region and retry");
        store.TryGet("source", out McpCapture retained).Should().BeTrue();
        retained.Should().BeSameAs(source);
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

    [TestMethod]
    public void AnnotateWithArrow_PreservesMixedDpiMonitorSegments()
    {
        var captureStore = new InMemoryMcpCaptureStore();
        McpCapture sourceCapture = CreateStoredCapture("capture:mixed", new Size(300, 200), mixedDpi: true);
        captureStore.Store(sourceCapture);
        var annotationService = new AnnotationService(
            captureStore,
            new AnnotationDrawableFactory(),
            new ManualTimeProvider(AnnotationTime));

        McpCapture annotated = annotationService.AnnotateWithArrow("capture:mixed", 20, 30, 220, 150, null);

        annotated.Metadata.Dpi.Should().BeNull();
        annotated.Metadata.Scale.Should().BeNull();
        annotated.Metadata.IsDpiScaleUniform.Should().BeFalse();
        annotated.Metadata.MonitorSegments.Should().BeEquivalentTo(sourceCapture.Metadata.MonitorSegments);
    }

    private static McpCapture CreateStoredCapture(string captureId, Size size, bool mixedDpi = false)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        MonitorSegmentDto[]? monitorSegments = mixedDpi
            ? [
                new("hmonitor:1", new RectangleDto(0, 0, 150, 200), new RectangleDto(0, 0, 150, 200), 96, 1, true),
                new("hmonitor:2", new RectangleDto(150, 0, 150, 200), new RectangleDto(150, 0, 150, 200), 144, 1.5f, false),
            ]
            : null;
        var metadata = McpCaptureMetadata.Create(
            captureId,
            AnnotationTime,
            size.Width,
            size.Height,
            dpi: mixedDpi ? null : 96,
            scale: mixedDpi ? null : 1,
            new Rectangle(Point.Empty, size),
            "region",
            "png",
            monitorSegments: monitorSegments);

        return new McpCapture(stream.ToArray(), metadata);
    }
}
