using CaptureTool.Application.Abstractions.Edit.Image.Description;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsImageDescriptionServiceTests
{
    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, ImageDescriptionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, ImageDescriptionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, ImageDescriptionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.DisabledByUser, ImageDescriptionReadyState.Disabled)]
    public void MapReadyState_ReturnsExpectedState(
        AIFeatureReadyState source,
        ImageDescriptionReadyState expected)
    {
        Assert.AreEqual(expected, WindowsImageDescriptionService.MapReadyState(source));
    }

    [TestMethod]
    [DataRow(ImageDescriptionMode.Brief, ImageDescriptionKind.BriefDescription)]
    [DataRow(ImageDescriptionMode.Detailed, ImageDescriptionKind.DetailedDescription)]
    [DataRow(ImageDescriptionMode.Diagram, ImageDescriptionKind.DiagramDescription)]
    [DataRow(ImageDescriptionMode.Accessible, ImageDescriptionKind.AccessibleDescription)]
    public void MapMode_ReturnsExpectedDescriptionKind(
        ImageDescriptionMode source,
        ImageDescriptionKind expected)
    {
        Assert.AreEqual(expected, WindowsImageDescriptionService.MapMode(source));
    }
}
