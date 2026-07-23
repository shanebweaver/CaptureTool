using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using Microsoft.Windows.AI;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsImageObjectEraseServiceTests
{
    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, AIFeatureReadyState.Ready, ObjectEraseReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, AIFeatureReadyState.Ready, ObjectEraseReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.Ready, AIFeatureReadyState.NotReady, ObjectEraseReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, AIFeatureReadyState.Ready, ObjectEraseReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.Ready, AIFeatureReadyState.DisabledByUser, ObjectEraseReadyState.Disabled)]
    public void MapReadyStates_ReturnsExpectedState(
        AIFeatureReadyState extractorState,
        AIFeatureReadyState removerState,
        ObjectEraseReadyState expected)
    {
        Assert.AreEqual(
            expected,
            WindowsImageObjectEraseService.MapReadyStates(extractorState, removerState));
    }

    [TestMethod]
    public void ScaleObjectPoint_MapsCanvasCoordinatesToBitmapCoordinates()
    {
        global::Windows.Graphics.PointInt32 result = WindowsImageObjectEraseService.ScaleObjectPoint(
            new Point(50, 25),
            new Size(100, 50),
            400,
            200);

        Assert.AreEqual(200, result.X);
        Assert.AreEqual(100, result.Y);
    }

    [TestMethod]
    public void GetOutputFileName_UsesObjectErasedSuffix()
    {
        Assert.AreEqual(
            "capture.object-erased.png",
            WindowsImageObjectEraseService.GetOutputFileName(@"C:\Images\capture.jpg"));
    }
}
