using CaptureTool.Application.Abstractions.Edit.Image.Description;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsImageDescriptionServiceTests
{
    [TestMethod]
    public void ModelDescriptor_ShouldExposeBoundedWindowsAiProvenance()
    {
        var service = new WindowsImageDescriptionService();

        Assert.IsInstanceOfType<IImageDescriptionService>(service);
        Assert.IsInstanceOfType<IImageDescriptionAnalysisService>(service);
        Assert.AreEqual("microsoft-windows", service.ModelDescriptor.ProducerId);
        Assert.AreEqual("windows-app-sdk-image-description", service.ModelDescriptor.ModelId);
        Assert.IsNull(service.ModelDescriptor.ModelVersion);
        Assert.AreEqual("windows-app-sdk-ai", service.ModelDescriptor.RuntimeId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(service.ModelDescriptor.RuntimeVersion));
        Assert.AreEqual(
            service.ModelDescriptor.RuntimeVersion,
            service.ModelDescriptor.PackageVersion);
    }

    [TestMethod]
    public async Task DescribeAnalysisAsync_WhenAlreadyCancelled_ShouldNotInvokeWindowsAi()
    {
        var service = new WindowsImageDescriptionService();
        using var source = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.DescribeAnalysisAsync(source, cancellation.Token));
    }

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
