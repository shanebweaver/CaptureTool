using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class Win2DImageCanvasExporterTests
{
    [TestMethod]
    public async Task RenderToStreamAsync_WithoutCanvasPreparedResource_RendersSourceImage()
    {
        string sourcePath = Path.Combine(TestContext.TestRunDirectory!, $"source-{Guid.NewGuid():N}.png");
        try
        {
            using (var source = new Bitmap(2, 2))
            {
                source.SetPixel(0, 0, Color.Red);
                source.SetPixel(1, 0, Color.Red);
                source.SetPixel(0, 1, Color.Red);
                source.SetPixel(1, 1, Color.Red);
                source.Save(sourcePath, ImageFormat.Png);
            }

            var drawable = new ImageDrawable(Vector2.Zero, new ImageFile(sourcePath), new Size(2, 2));
            var options = new ImageCanvasRenderOptions(
                ImageOrientation.RotateNoneFlipNone,
                new Size(2, 2),
                new Rectangle(0, 0, 2, 2));
            var exporter = new Win2DImageCanvasExporter(Mock.Of<IClipboardService>());

            using MemoryStream rendered = await exporter.RenderToStreamAsync([drawable], options);
            using var result = new Bitmap(rendered);

            result.GetPixel(0, 0).ToArgb().Should().Be(Color.Red.ToArgb());
            drawable.GetPreparedImage().Should().BeNull();
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [TestMethod]
    public async Task RenderToStreamAsync_WhenSourceImageIsMissing_FailsExplicitly()
    {
        string sourcePath = Path.Combine(TestContext.TestRunDirectory!, $"missing-{Guid.NewGuid():N}.png");
        var drawable = new ImageDrawable(Vector2.Zero, new ImageFile(sourcePath), new Size(2, 2));
        var options = new ImageCanvasRenderOptions(
            ImageOrientation.RotateNoneFlipNone,
            new Size(2, 2),
            new Rectangle(0, 0, 2, 2));
        var exporter = new Win2DImageCanvasExporter(Mock.Of<IClipboardService>());

        Func<Task> render = async () =>
        {
            using MemoryStream _ = await exporter.RenderToStreamAsync([drawable], options);
        };

        await render.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{sourcePath}*");
    }

    public TestContext TestContext { get; set; } = null!;
}
