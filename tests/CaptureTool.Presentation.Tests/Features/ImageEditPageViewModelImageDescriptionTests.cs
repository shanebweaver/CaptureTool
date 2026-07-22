using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelImageDescriptionTests
{
    [TestMethod]
    public async Task LoadAsync_WhenFeatureDisabled_ShouldHideAndDisableMode()
    {
        var service = new Mock<IImageDescriptionService>(MockBehavior.Strict);
        ImageEditPageViewModel viewModel = CreateViewModel(
            imageDescriptionService: service.Object,
            featureAvailability: Mock.Of<IImageDescriptionFeatureAvailability>(x => x.IsImageDescriptionEnabled == false));

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.IsImageDescriptionFeatureEnabled.Should().BeFalse();
        viewModel.IsImageDescriptionAvailable.Should().BeFalse();
        viewModel.CanToggleImageDescription.Should().BeFalse();
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ToggleMode_WhenConsentRefused_ShouldRemainInactiveAndRefreshToggle()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        var service = new Mock<IImageDescriptionService>();

        consent
            .Setup(x => x.GetConsentState(AiFeatureId.ImageDescription))
            .Returns(AiFeatureConsentState.Denied);
        consent
            .Setup(x => x.SetConsentAsync(AiFeatureId.ImageDescription, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dialog
            .Setup(x => x.RequestConsentAsync(AiFeatureId.ImageDescription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageDescriptionService: service.Object,
            consentService: consent.Object,
            consentDialogService: dialog.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);

        viewModel.IsImageDescriptionModeActive.Should().BeFalse();
        viewModel.CanToggleImageDescription.Should().BeTrue();
        changedProperties.Should().Contain(nameof(ImageEditPageViewModel.IsImageDescriptionModeActive));
        service.Verify(
            x => x.DescribeAsync(It.IsAny<ImageDescriptionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ToggleMode_WhenConsentAccepted_ShouldEnableModeWithoutRunningModel()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        var service = new Mock<IImageDescriptionService>();

        consent
            .Setup(x => x.GetConsentState(AiFeatureId.ImageDescription))
            .Returns(AiFeatureConsentState.Unknown);
        consent
            .Setup(x => x.SetConsentAsync(AiFeatureId.ImageDescription, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dialog
            .Setup(x => x.RequestConsentAsync(AiFeatureId.ImageDescription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageDescriptionService: service.Object,
            consentService: consent.Object,
            consentDialogService: dialog.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);

        viewModel.IsImageDescriptionModeActive.Should().BeTrue();
        viewModel.CanGenerateImageDescription.Should().BeTrue();
        service.Verify(
            x => x.DescribeAsync(It.IsAny<ImageDescriptionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DetailedCommand_ShouldDescribeCurrentRenderedImageInMemory()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        byte[] renderedBytes = [1, 2, 3, 4];

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<CaptureTool.Domain.Edit.Drawable.IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(new MemoryStream(renderedBytes));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.Is<ImageDescriptionRequest>(request => IsExpectedRequest(request, renderedBytes)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageDescriptionResult.Success("A detailed description."));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);

        viewModel.ImageDescription.Should().Be("A detailed description.");
        viewModel.HasImageDescription.Should().BeTrue();
        exporter.Verify(x => x.SaveImageAsync(
            It.IsAny<string>(),
            It.IsAny<CaptureTool.Domain.Edit.Drawable.IDrawable[]>(),
            It.IsAny<ImageCanvasRenderOptions>()), Times.Never);
    }

    private static bool IsExpectedRequest(ImageDescriptionRequest request, byte[] renderedBytes)
    {
        return request.Mode == ImageDescriptionMode.Detailed &&
            request.SourceSize == new Size(100, 50) &&
            request.SourceImage is MemoryStream stream &&
            stream.ToArray().SequenceEqual(renderedBytes);
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageCanvasExporter? imageCanvasExporter = null,
        IImageDescriptionService? imageDescriptionService = null,
        IImageDescriptionFeatureAvailability? featureAvailability = null,
        IAiFeatureConsentService? consentService = null,
        IAiFeatureConsentDialogService? consentDialogService = null)
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(x => x.GetImageFileSize(It.IsAny<ImageFile>()))
            .Returns(new Size(100, 50));

        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(x => x.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());

        ILocalizationService localization = Mock.Of<ILocalizationService>();
        IAppNotificationService notifications = Mock.Of<IAppNotificationService>();

        return new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            imageCanvasExporter ?? Mock.Of<IImageCanvasExporter>(),
            Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == false),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            new ColorPickerToolViewModel(Mock.Of<IClipboardService>(), localization, notifications),
            new ChromaKeyToolViewModel(
                Mock.Of<IChromaKeyAccessService>(x => x.IsChromaKeyEnabled == false),
                Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(Mock.Of<IClipboardService>(), localization, notifications),
            consentService ?? Mock.Of<IAiFeatureConsentService>(x =>
                x.GetConsentState(AiFeatureId.ImageDescription) == AiFeatureConsentState.Granted),
            consentDialogService ?? Mock.Of<IAiFeatureConsentDialogService>(),
            null,
            null,
            imageDescriptionService ?? Mock.Of<IImageDescriptionService>(x =>
                x.GetReadyState() == ImageDescriptionReadyState.Ready),
            featureAvailability ?? Mock.Of<IImageDescriptionFeatureAvailability>(x =>
                x.IsImageDescriptionEnabled == true));
    }
}
