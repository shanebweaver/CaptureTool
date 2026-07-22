using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsImageSuperResolutionServiceTests
{
    [TestMethod]
    public void CalculateTargetSize_ShouldUseRequestedScale_WhenWithinSupportedLimits()
    {
        Size result = WindowsImageSuperResolutionService.CalculateTargetSize(
            new Size(120, 80),
            requestedScaleFactor: 2,
            scalerMaxSupportedScaleFactor: 8);

        result.Should().Be(new Size(240, 160));
    }

    [TestMethod]
    public void CalculateTargetSize_ShouldCapToScalerMaxSupportedScale()
    {
        Size result = WindowsImageSuperResolutionService.CalculateTargetSize(
            new Size(120, 80),
            requestedScaleFactor: 4,
            scalerMaxSupportedScaleFactor: 2);

        result.Should().Be(new Size(240, 160));
    }

    [TestMethod]
    public void CalculateTargetSize_ShouldCapToDocumentedEightXLimit()
    {
        Size result = WindowsImageSuperResolutionService.CalculateTargetSize(
            new Size(120, 80),
            requestedScaleFactor: 12,
            scalerMaxSupportedScaleFactor: 12);

        result.Should().Be(new Size(960, 640));
    }

    [TestMethod]
    public void GetOutputFileName_ShouldUseStableSuperSuffix()
    {
        string firstName = WindowsImageSuperResolutionService.GetOutputFileName(@"C:\captures\example.png");
        string secondName = WindowsImageSuperResolutionService.GetOutputFileName(@"C:\captures\example.png");

        firstName.Should().Be("example.super.png");
        secondName.Should().Be(firstName);
    }
}
