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
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Numerics;

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

    [TestMethod]
    public async Task CopyImageDescriptionCommand_ShouldCopyOnlyWhenDescriptionIsVisible()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        var clipboard = new Mock<IClipboardService>();

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.IsAny<ImageDescriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageDescriptionResult.Success("A detailed description."));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object,
            clipboardService: clipboard.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        viewModel.CopyImageDescriptionCommand.CanExecute(null).Should().BeFalse();
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);

        viewModel.CopyImageDescriptionCommand.CanExecute(null).Should().BeTrue();
        await viewModel.CopyImageDescriptionCommand.ExecuteAsync(null);

        clipboard.Verify(
            service => service.CopyTextAsync("A detailed description."),
            Times.Once);

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        viewModel.CopyImageDescriptionCommand.CanExecute(null).Should().BeFalse();
    }

    [TestMethod]
    public async Task ClosingMode_ShouldCancelRequestAndAllowSameModeToRunAfterReopening()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        var firstRequest = new TaskCompletionSource<ImageDescriptionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstRequestToken = default;
        int requestCount = 0;

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.IsAny<ImageDescriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns((ImageDescriptionRequest _, CancellationToken cancellationToken) =>
            {
                requestCount++;
                if (requestCount == 1)
                {
                    firstRequestToken = cancellationToken;
                    return firstRequest.Task;
                }

                return Task.FromResult(ImageDescriptionResult.Success("A new description."));
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        Task originalExecution = viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);
        requestCount.Should().Be(1);

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);

        firstRequestToken.IsCancellationRequested.Should().BeTrue();
        viewModel.IsImageDescriptionRunning.Should().BeFalse();
        viewModel.ImageDescription.Should().BeEmpty();
        viewModel.SelectedImageDescriptionMode.Should().BeNull();

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        viewModel.SelectedImageDescriptionMode.Should().BeNull();
        viewModel.GenerateDetailedImageDescriptionCommand.CanExecute(null).Should().BeTrue();
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);

        viewModel.ImageDescription.Should().Be("A new description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();
        requestCount.Should().Be(2);

        firstRequest.SetResult(ImageDescriptionResult.Cancelled);
        await originalExecution;

        viewModel.ImageDescription.Should().Be("A new description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();
    }

    [TestMethod]
    public async Task RunningMode_ShouldDisableOnlyItsButtonAndAllowSwitchingModes()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        var detailedRequest = new TaskCompletionSource<ImageDescriptionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken detailedRequestToken = default;
        int requestCount = 0;

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.IsAny<ImageDescriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns((ImageDescriptionRequest request, CancellationToken cancellationToken) =>
            {
                requestCount++;
                if (request.Mode == ImageDescriptionMode.Detailed)
                {
                    detailedRequestToken = cancellationToken;
                    return detailedRequest.Task;
                }

                return Task.FromResult(ImageDescriptionResult.Success("A brief description."));
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        Task detailedExecution = viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);

        viewModel.CanGenerateImageDescription.Should().BeTrue();
        viewModel.GenerateDetailedImageDescriptionCommand.CanExecute(null).Should().BeFalse();
        viewModel.GenerateBriefImageDescriptionCommand.CanExecute(null).Should().BeTrue();
        viewModel.GenerateDiagramImageDescriptionCommand.CanExecute(null).Should().BeTrue();
        viewModel.GenerateAccessibleImageDescriptionCommand.CanExecute(null).Should().BeTrue();

        await viewModel.GenerateBriefImageDescriptionCommand.ExecuteAsync(null);

        detailedRequestToken.IsCancellationRequested.Should().BeTrue();
        viewModel.ImageDescription.Should().Be("A brief description.");
        viewModel.IsBriefImageDescriptionSelected.Should().BeTrue();
        viewModel.IsDetailedImageDescriptionSelected.Should().BeFalse();
        requestCount.Should().Be(2);

        detailedRequest.SetResult(ImageDescriptionResult.Cancelled);
        await detailedExecution;

        viewModel.ImageDescription.Should().Be("A brief description.");
        viewModel.IsBriefImageDescriptionSelected.Should().BeTrue();
    }

    [TestMethod]
    public async Task SelectingUncachedMode_ShouldImmediatelyClearDisplayedDescriptionAndSelection()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        var briefRequest = new TaskCompletionSource<ImageDescriptionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.IsAny<ImageDescriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns((ImageDescriptionRequest request, CancellationToken _) =>
                request.Mode == ImageDescriptionMode.Detailed
                    ? Task.FromResult(ImageDescriptionResult.Success("A detailed description."))
                    : briefRequest.Task);

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().Be("A detailed description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();

        Task briefExecution = viewModel.GenerateBriefImageDescriptionCommand.ExecuteAsync(null);

        viewModel.ImageDescription.Should().BeEmpty();
        viewModel.HasImageDescription.Should().BeFalse();
        viewModel.SelectedImageDescriptionMode.Should().BeNull();
        viewModel.IsBriefImageDescriptionSelected.Should().BeFalse();
        viewModel.IsDetailedImageDescriptionSelected.Should().BeFalse();

        briefRequest.SetResult(ImageDescriptionResult.Success("A brief description."));
        await briefExecution;

        viewModel.ImageDescription.Should().Be("A brief description.");
        viewModel.IsBriefImageDescriptionSelected.Should().BeTrue();
    }

    [TestMethod]
    public async Task DescriptionModes_ShouldCacheResultsUntilImageChanges()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var service = new Mock<IImageDescriptionService>();
        int requestCount = 0;

        exporter
            .Setup(x => x.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageDescriptionReadyState.Ready);
        service
            .Setup(x => x.DescribeAsync(
                It.IsAny<ImageDescriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImageDescriptionRequest request, CancellationToken _) =>
            {
                requestCount++;
                return ImageDescriptionResult.Success(request.Mode switch
                {
                    ImageDescriptionMode.Brief => "A brief description.",
                    ImageDescriptionMode.Detailed => "A detailed description.",
                    _ => "Another description."
                });
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            imageDescriptionService: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);

        viewModel.ImageDescription.Should().Be("A detailed description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().BeEmpty();
        viewModel.SelectedImageDescriptionMode.Should().BeNull();
        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().BeEmpty();
        viewModel.SelectedImageDescriptionMode.Should().BeNull();
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().Be("A detailed description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();
        requestCount.Should().Be(1);

        await viewModel.GenerateBriefImageDescriptionCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().Be("A brief description.");
        viewModel.IsBriefImageDescriptionSelected.Should().BeTrue();
        viewModel.IsDetailedImageDescriptionSelected.Should().BeFalse();
        requestCount.Should().Be(2);

        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);
        viewModel.ImageDescription.Should().Be("A detailed description.");
        viewModel.IsDetailedImageDescriptionSelected.Should().BeTrue();
        viewModel.IsBriefImageDescriptionSelected.Should().BeFalse();
        requestCount.Should().Be(2);

        viewModel.AddDrawable(new RectangleDrawable(
            new Vector2(1, 1),
            new Size(10, 10),
            Color.Red,
            Color.Transparent,
            1));

        viewModel.IsImageDescriptionModeActive.Should().BeFalse();
        viewModel.ImageDescription.Should().BeEmpty();
        viewModel.SelectedImageDescriptionMode.Should().BeNull();

        await viewModel.ToggleImageDescriptionModeCommand.ExecuteAsync(null);
        await viewModel.GenerateDetailedImageDescriptionCommand.ExecuteAsync(null);
        requestCount.Should().Be(3);
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
        IAiFeatureConsentDialogService? consentDialogService = null,
        IClipboardService? clipboardService = null)
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
            clipboardService ?? Mock.Of<IClipboardService>(),
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
