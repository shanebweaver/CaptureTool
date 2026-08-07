using CaptureTool.Presentation.Features.ImageEdit;
using FluentAssertions;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImagePointSelectionInteractionControllerTests
{
    [TestMethod]
    public void ResolveMode_ShouldPreserveExistingToolPriority()
    {
        var controller = new ImagePointSelectionInteractionController();

        ImagePointSelectionMode mode = controller.ResolveMode(
            isForegroundExtractionEnabled: true,
            isObjectEraseEnabled: true,
            isObjectExtractionEnabled: true);

        mode.Should().Be(ImagePointSelectionMode.ObjectExtraction);
        controller.IsActive.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void TrySelect_ShouldRejectInvalidPointerInput(
        bool isPrimaryButtonPressed,
        bool isInsideImage,
        bool isModeActive)
    {
        var controller = new ImagePointSelectionInteractionController();
        controller.ResolveMode(
            isForegroundExtractionEnabled: isModeActive,
            isObjectEraseEnabled: false,
            isObjectExtractionEnabled: false);

        bool accepted = controller.TrySelect(
            isPrimaryButtonPressed,
            isInsideImage,
            new Vector2(10, 20),
            out ImagePointSelectionRequest request);

        accepted.Should().BeFalse();
        request.Should().Be(default(ImagePointSelectionRequest));
    }

    [TestMethod]
    public void TrySelect_ShouldReturnActiveModeAndPosition()
    {
        var controller = new ImagePointSelectionInteractionController();
        var position = new Vector2(30, 40);
        controller.ResolveMode(
            isForegroundExtractionEnabled: false,
            isObjectEraseEnabled: true,
            isObjectExtractionEnabled: false);

        bool accepted = controller.TrySelect(
            isPrimaryButtonPressed: true,
            isInsideImage: true,
            position,
            out ImagePointSelectionRequest request);

        accepted.Should().BeTrue();
        request.Should().Be(new ImagePointSelectionRequest(ImagePointSelectionMode.ObjectErase, position));
    }
}
