using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using Microsoft.Windows.AI;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsAiTextExtractionServiceTests
{
    [TestMethod]
    public void ModelDescriptor_ShouldExposeWindowsAiProvenance()
    {
        var service = new WindowsAiTextExtractionService();

        Assert.AreEqual("microsoft-windows", service.ModelDescriptor.ProducerId);
        Assert.AreEqual("windows-app-sdk-text-recognizer", service.ModelDescriptor.ModelId);
        Assert.AreEqual("windows-app-sdk-ai", service.ModelDescriptor.RuntimeId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(service.ModelDescriptor.RuntimeVersion));
    }

    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, TextExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, TextExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.NotCompatibleWithSystemHardware, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.CapabilityMissing, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.OSUpdateNeeded, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.DisabledByUser, TextExtractionReadyState.Disabled)]
    public void MapReadyState_ReturnsExpectedState(
        AIFeatureReadyState source,
        TextExtractionReadyState expected)
    {
        Assert.AreEqual(expected, WindowsAiTextExtractionService.MapReadyState(source));
    }

    [TestMethod]
    public async Task ExtractAnalysisAsync_WhenAlreadyCancelled_DoesNotInvokeWindowsAi()
    {
        var service = new WindowsAiTextExtractionService();
        using var source = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.ExtractAnalysisAsync(source, cancellation.Token));
    }
}
