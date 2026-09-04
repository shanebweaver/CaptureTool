using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

internal static class CaptureAnalyzerContractAssertions
{
    public static void AssertOnDeviceImageAnalyzer(
        ICaptureAnalyzer analyzer,
        CapabilityDefinition expectedCapability)
    {
        CaptureAnalyzerDescriptor descriptor = analyzer.Descriptor;

        Assert.AreEqual(expectedCapability, descriptor.Capability);
        Assert.AreEqual(ProcessingBoundary.OnDevice, descriptor.ProcessingBoundary);
        Assert.AreEqual(CaptureAnalyzerDataKind.None, descriptor.DataSent);
        Assert.IsFalse(descriptor.Identity.Revision.IsEmpty);
        Assert.IsGreaterThanOrEqualTo(0, descriptor.QualityTier);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Image },
            descriptor.SupportedMediaKinds.ToArray());
    }

    public static void AssertCompatibleSuccess(
        ICaptureAnalyzer analyzer,
        CaptureAnalyzerOutput output)
    {
        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        Assert.IsTrue(output.IsCompatibleWith(analyzer.Descriptor));
        Assert.IsNotNull(output.Payload);
        Assert.IsNull(output.Failure);
    }
}
