using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsTextExtractionServiceTests
{
    [TestMethod]
    public void ModelDescriptor_ShouldExposeBoundedWindowsOcrProvenance()
    {
        var service = new WindowsTextExtractionService();

        Assert.IsInstanceOfType<ITextExtractionService>(service);
        Assert.IsInstanceOfType<ITextExtractionAnalysisService>(service);
        Assert.AreEqual("microsoft-windows", service.ModelDescriptor.ProducerId);
        Assert.AreEqual("windows-media-ocr", service.ModelDescriptor.ModelId);
        Assert.IsNull(service.ModelDescriptor.ModelVersion);
        Assert.AreEqual("windows-media-ocr", service.ModelDescriptor.RuntimeId);
        Assert.IsNull(service.ModelDescriptor.RuntimeVersion);
    }

    [TestMethod]
    public async Task ExtractAnalysisAsync_WhenAlreadyCancelled_ShouldNotInvokeWindowsOcr()
    {
        var service = new WindowsTextExtractionService();
        using var source = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.ExtractAnalysisAsync(source, cancellation.Token));
    }
}
