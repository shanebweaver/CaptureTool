using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class TextExtractionToolViewModelTests
{
    [TestMethod]
    public async Task CopyAllTextCommand_WhenTextIsAvailable_CopiesCompleteDocument()
    {
        var clipboard = new Mock<IClipboardService>();
        var notifications = new Mock<IAppNotificationService>();
        TextExtractionToolViewModel viewModel = CreateViewModel(clipboard, notifications);
        viewModel.SetText("First line" + Environment.NewLine + "Second line");

        await viewModel.CopyAllTextCommand.ExecuteAsync(null);

        clipboard.Verify(service => service.CopyTextAsync(
            "First line" + Environment.NewLine + "Second line"), Times.Once);
        notifications.Verify(service => service.ShowInfo("Recognized text copied."), Times.Once);
    }

    [TestMethod]
    public void SetText_WhenTextIsEmpty_DisablesCopyAllTextCommand()
    {
        TextExtractionToolViewModel viewModel = CreateViewModel(
            new Mock<IClipboardService>(),
            new Mock<IAppNotificationService>());

        viewModel.SetText("text");
        viewModel.CopyAllTextCommand.CanExecute(null).Should().BeTrue();

        viewModel.Reset();

        viewModel.CopyAllTextCommand.CanExecute(null).Should().BeFalse();
    }

    private static TextExtractionToolViewModel CreateViewModel(
        Mock<IClipboardService> clipboard,
        Mock<IAppNotificationService> notifications)
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.GetString("ImageEdit_TextCopiedNotification"))
            .Returns("Recognized text copied.");

        return new TextExtractionToolViewModel(
            clipboard.Object,
            localization.Object,
            notifications.Object);
    }
}
