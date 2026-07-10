using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ColorPickerToolViewModelTests
{
    [TestMethod]
    public void UpdatePickedColor_ShouldFormatHexByDefault()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdatePickedColor(Color.FromArgb(12, 34, 56));

        viewModel.PickedColorValue.Should().Be("#0C2238");
    }

    [TestMethod]
    public void UpdateSelectedColorTypeIndex_ShouldReformatPickedColor()
    {
        var viewModel = CreateViewModel();
        viewModel.UpdatePickedColor(Color.Red);

        viewModel.UpdateSelectedColorTypeIndex((int)ColorPickerColorType.Rgb);

        viewModel.PickedColorValue.Should().Be("rgb(255, 0, 0)");

        viewModel.UpdateSelectedColorTypeIndex((int)ColorPickerColorType.Hsl);

        viewModel.PickedColorValue.Should().Be("hsl(0, 100%, 50%)");
    }

    [TestMethod]
    public async Task CopyPickedColorAsync_ShouldCopyFormattedValue_AndShowInfo()
    {
        var clipboard = new Mock<IClipboardService>();
        var notifications = new Mock<IAppNotificationService>();
        var viewModel = CreateViewModel(clipboard.Object, notifications.Object);
        viewModel.UpdatePickedColor(Color.Blue);

        await viewModel.CopyPickedColorAsync();

        clipboard.Verify(service => service.CopyTextAsync("#0000FF"), Times.Once);
        notifications.Verify(service => service.ShowInfo("Color copied"), Times.Once);
    }

    [TestMethod]
    public async Task CopyPickedColorAsync_WithoutPickedColor_ShouldDoNothing()
    {
        var clipboard = new Mock<IClipboardService>();
        var notifications = new Mock<IAppNotificationService>();
        var viewModel = CreateViewModel(clipboard.Object, notifications.Object);

        await viewModel.CopyPickedColorAsync();

        clipboard.Verify(service => service.CopyTextAsync(It.IsAny<string>()), Times.Never);
        notifications.Verify(service => service.ShowInfo(It.IsAny<string>()), Times.Never);
    }

    private static ColorPickerToolViewModel CreateViewModel(
        IClipboardService? clipboard = null,
        IAppNotificationService? notifications = null)
    {
        return new(
            clipboard ?? Mock.Of<IClipboardService>(),
            notifications ?? Mock.Of<IAppNotificationService>());
    }
}
