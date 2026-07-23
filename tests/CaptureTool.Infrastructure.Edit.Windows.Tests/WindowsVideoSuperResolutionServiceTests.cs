using FluentAssertions;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsVideoSuperResolutionServiceTests
{
    [TestMethod]
    public void CalculateTargetSize_ShouldUseRequestedScale_WhenWithinOutputLimits()
    {
        (int width, int height) = WindowsVideoSuperResolutionService.CalculateTargetSize(
            1280,
            720,
            requestedScaleFactor: 2);

        width.Should().Be(2560);
        height.Should().Be(1440);
    }

    [TestMethod]
    public void CalculateTargetSize_ShouldCapLandscapeVideoAtFourK()
    {
        (int width, int height) = WindowsVideoSuperResolutionService.CalculateTargetSize(
            2560,
            1440,
            requestedScaleFactor: 2);

        width.Should().Be(3840);
        height.Should().Be(2160);
    }

    [TestMethod]
    public void CalculateTargetSize_ShouldKeepEncoderDimensionsEven()
    {
        (int width, int height) = WindowsVideoSuperResolutionService.CalculateTargetSize(
            853,
            480,
            requestedScaleFactor: 2);

        width.Should().Be(1706);
        height.Should().Be(960);
        (width % 2).Should().Be(0);
        (height % 2).Should().Be(0);
    }

    [TestMethod]
    public void GetOutputFileName_ShouldUseStableSuperSuffix()
    {
        string outputFileName = WindowsVideoSuperResolutionService.GetOutputFileName(
            @"C:\captures\example.mp4");

        outputFileName.Should().Be("example.super.mp4");
    }
}
