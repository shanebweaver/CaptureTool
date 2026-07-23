using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using Microsoft.Windows.AI;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsImageForegroundExtractionServiceTests
{
    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, ForegroundExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, ForegroundExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, ForegroundExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.DisabledByUser, ForegroundExtractionReadyState.Disabled)]
    public void MapReadyState_ReturnsExpectedState(
        AIFeatureReadyState source,
        ForegroundExtractionReadyState expected)
    {
        Assert.AreEqual(expected, WindowsImageForegroundExtractionService.MapReadyState(source));
    }

    [TestMethod]
    public void ScaleForegroundPoint_MapsCanvasCoordinatesToBitmapCoordinates()
    {
        global::Windows.Graphics.PointInt32 result = WindowsImageForegroundExtractionService.ScaleForegroundPoint(
            new Point(50, 25),
            new Size(100, 50),
            400,
            200);

        Assert.AreEqual(200, result.X);
        Assert.AreEqual(100, result.Y);
    }

    [TestMethod]
    public void ApplyMaskToAlpha_CombinesMaskWithExistingTransparency()
    {
        byte[] pixels =
        [
            10, 20, 30, 255,
            40, 50, 60, 128,
            70, 80, 90, 255
        ];
        byte[] mask = [255, 128, 0];

        WindowsImageForegroundExtractionService.ApplyMaskToAlpha(pixels, mask);

        CollectionAssert.AreEqual(
            new byte[]
            {
                10, 20, 30, 255,
                40, 50, 60, 64,
                70, 80, 90, 0
            },
            pixels);
    }

    [TestMethod]
    public void GetOutputFileName_UsesForegroundSuffix()
    {
        Assert.AreEqual(
            "capture.foreground.png",
            WindowsImageForegroundExtractionService.GetOutputFileName(@"C:\Images\capture.jpg"));
    }
}
