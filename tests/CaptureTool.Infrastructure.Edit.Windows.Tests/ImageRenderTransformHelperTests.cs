using CaptureTool.Domain.Edit;
using FluentAssertions;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class ImageRenderTransformHelperTests
{
    [TestMethod]
    public void CalculateRenderTransform_WithDefaultOrientation_ReturnsIdentity()
    {
        var transform = ImageRenderTransformHelper.CalculateRenderTransform(
            new Size(400, 300),
            ImageOrientation.RotateNoneFlipNone);

        transform.Should().Be(Matrix3x2.Identity);
    }

    [TestMethod]
    public void CalculateRenderTransform_WithScale_ReturnsScaleMatrix()
    {
        var transform = ImageRenderTransformHelper.CalculateRenderTransform(
            new Size(400, 300),
            ImageOrientation.RotateNoneFlipNone,
            scale: 2f);

        transform.Should().Be(Matrix3x2.CreateScale(2f));
    }
}
