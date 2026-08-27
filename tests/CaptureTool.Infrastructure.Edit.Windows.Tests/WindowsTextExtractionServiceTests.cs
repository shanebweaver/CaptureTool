using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using WinPoint = Windows.Foundation.Point;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsTextExtractionServiceTests
{
    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, false, TextExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, false, TextExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotReady, true, TextExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, false, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, true, TextExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.DisabledByUser, false, TextExtractionReadyState.Disabled)]
    [DataRow(AIFeatureReadyState.DisabledByUser, true, TextExtractionReadyState.Ready)]
    public void GetCombinedReadyState_ReturnsExpectedState(
        AIFeatureReadyState source,
        bool isLegacyOcrAvailable,
        TextExtractionReadyState expected)
    {
        Assert.AreEqual(
            expected,
            WindowsTextExtractionService.GetCombinedReadyState(source, isLegacyOcrAvailable));
    }

    [TestMethod]
    public void ToRectangleF_ReturnsAxisAlignedBoundsForRotatedText()
    {
        var bounds = new RecognizedTextBoundingBox
        {
            TopLeft = new WinPoint(20, 10),
            TopRight = new WinPoint(80, 20),
            BottomRight = new WinPoint(70, 50),
            BottomLeft = new WinPoint(10, 40)
        };

        RectangleF result = WindowsTextExtractionService.ToRectangleF(bounds);

        Assert.AreEqual(RectangleF.FromLTRB(10, 10, 80, 50), result);
    }
}
