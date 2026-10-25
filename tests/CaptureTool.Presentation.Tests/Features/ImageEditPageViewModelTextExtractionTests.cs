using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
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
public sealed class ImageEditPageViewModelTextExtractionTests
{
    [TestMethod]
    public async Task PaneExtractionUsesRenderedEditsAndCachesForTheSessionWithoutEnteringAnotherMode()
    {
        var exporter = CreateExporter();
        var extraction = new Mock<ITextExtractionService>();
        extraction.Setup(x => x.GetReadyState()).Returns(TextExtractionReadyState.Ready);
        extraction.Setup(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new("Cropped text", new(60, 30), [new("Cropped text", new(5, 4, 40, 10))], [])));
        using var vm = CreateViewModel(imageCanvasExporter: exporter.Object, textExtractionService: extraction.Object);
        await vm.LoadAsync(new ImageFile("capture.png"), CancellationToken.None);
        var oldCrop = vm.CropRect;
        vm.UpdateCropRectCommand.Execute(new Rectangle(10, 10, 60, 30));
        vm.OnCropInteractionComplete(oldCrop);
        await vm.EditorText.ExtractAsync();
        await vm.EditorText.ExtractAsync();
        Assert.IsTrue(vm.EditorText.HasChanges);
        Assert.AreEqual("Cropped text", vm.EditorText.Document!.Text);
        Assert.IsFalse(vm.HasActiveEditMode);
        exporter.Verify(x => x.RenderToStreamAsync(It.IsAny<IDrawable[]>(),
            It.Is<ImageCanvasRenderOptions>(options => options.CropRect == new Rectangle(10, 10, 60, 30))), Times.Once);
        extraction.Verify(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.RotateCommand.Execute(null);
        Assert.IsNull(vm.EditorText.Document);
        await vm.EditorText.ExtractAsync();
        extraction.Verify(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PaneCloseOrImageEditRejectsLateExtractionResults(bool edit)
    {
        var pending = new TaskCompletionSource<TextExtractionResult>();
        var extraction = new Mock<ITextExtractionService>();
        extraction.Setup(x => x.GetReadyState()).Returns(TextExtractionReadyState.Ready);
        CancellationToken token = default;
        extraction.Setup(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((TextExtractionRequest _, CancellationToken ct) => { token = ct; return pending.Task; });
        using var vm = CreateViewModel(imageCanvasExporter: CreateExporter().Object, textExtractionService: extraction.Object);
        await vm.LoadAsync(new ImageFile("capture.png"), CancellationToken.None);
        Task operation = vm.EditorText.ExtractAsync();
        if (edit) vm.RotateCommand.Execute(null);
        else vm.EditorText.Cancel();
        Assert.IsTrue(token.IsCancellationRequested);
        pending.SetResult(TextExtractionResult.Success(new("Late text", new(100, 50), [], [])));
        await operation;
        Assert.IsNull(vm.EditorText.Document);
        Assert.IsFalse(vm.EditorText.IsRunning);
    }

    [TestMethod]
    public async Task StandaloneOcrUsesSharedConsentAndItsOwnExtractionService()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        consent.Setup(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var extraction = new Mock<ITextExtractionService>();
        extraction.Setup(x => x.GetReadyState()).Returns(TextExtractionReadyState.Ready);
        extraction.Setup(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new("fresh OCR", new(100, 50), [])));
        using var vm = CreateViewModel(imageCanvasExporter: CreateExporter().Object, textExtractionService: extraction.Object,
            textExtractionConsent: consent.Object);
        await vm.LoadAsync(new ImageFile("new-capture.png"), CancellationToken.None);
        await vm.ToggleTextExtractionModeCommand.ExecuteAsync(null);
        Assert.AreEqual("fresh OCR", vm.TextExtractionTool.Text);
        consent.Verify(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RevokingConsentDuringAdHocOcrDiscardsEvenANonCooperativeProviderResult()
    {
        using var revoked = new CancellationTokenSource();
        var consent = new Mock<IAiFeatureConsentService>();
        consent.Setup(x => x.GetConsentState(AiFeatureId.TextExtraction)).Returns(AiFeatureConsentState.Granted);
        consent.Setup(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        consent.SetupGet(x => x.Revoked).Returns(revoked.Token);
        var extraction = new Mock<ITextExtractionService>();
        extraction.Setup(x => x.GetReadyState()).Returns(TextExtractionReadyState.Ready);
        var pending = new TaskCompletionSource<TextExtractionResult>();
        extraction.Setup(x => x.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var vm = CreateViewModel(imageCanvasExporter: CreateExporter().Object, textExtractionService: extraction.Object,
            textExtractionConsent: consent.Object);
        await vm.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        var operation = vm.ToggleTextExtractionModeCommand.ExecuteAsync(null);
        await revoked.CancelAsync();
        pending.SetResult(TextExtractionResult.Success(new("late", new(100, 50), [])));
        await operation;
        vm.TextExtractionTool.Text.Should().BeEmpty();
        vm.IsTextExtractionRunning.Should().BeFalse();
    }

    private static Mock<IImageCanvasExporter> CreateExporter()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        exporter.Setup(x => x.RenderToStreamAsync(It.IsAny<IDrawable[]>(), It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        return exporter;
    }

    [TestMethod]
    public async Task LoadAsync_WhenTextExtractionFeatureDisabled_ShouldHideAndDisableToggle()
    {
        var textExtraction = new Mock<ITextExtractionService>(MockBehavior.Strict);
        ImageEditPageViewModel viewModel = CreateViewModel(
            textExtractionService: textExtraction.Object,
            textExtractionFeatureAvailability: Mock.Of<ITextExtractionFeatureAvailability>(x => x.IsTextExtractionEnabled == false));

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.IsTextExtractionFeatureEnabled.Should().BeFalse();
        viewModel.IsTextExtractionAvailable.Should().BeFalse();
        viewModel.CanToggleTextExtraction.Should().BeFalse();
        textExtraction.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenFirstUseConsentDenied_ShouldPersistDenialAndNotExtract()
    {
        AiFeatureConsentState consentState = AiFeatureConsentState.Unknown;
        var consent = new Mock<IAiFeatureConsentService>();
        var textExtraction = new Mock<ITextExtractionService>();

        consent
            .Setup(service => service.GetConsentState(AiFeatureId.TextExtraction))
            .Returns(() => consentState);
        consent
            .Setup(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .Callback(() => consentState = AiFeatureConsentState.Denied)
            .ReturnsAsync(false);
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(
            textExtractionConsent: consent.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.CanToggleTextExtraction.Should().BeTrue();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.CanToggleTextExtraction.Should().BeTrue();
        changedProperties.Should().Contain(nameof(ImageEditPageViewModel.IsTextExtractionModeActive));
        consent.Verify(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
        textExtraction.Verify(service => service.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenConsentWasDenied_ShouldPromptAgainAndExtractWhenAccepted()
    {
        AiFeatureConsentState consentState = AiFeatureConsentState.Denied;
        var consent = new Mock<IAiFeatureConsentService>();
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();

        consent
            .Setup(service => service.GetConsentState(AiFeatureId.TextExtraction))
            .Returns(() => consentState);
        consent
            .Setup(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .Callback(() => consentState = AiFeatureConsentState.Granted)
            .ReturnsAsync(true);
        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.IsAny<TextExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new RecognizedTextDocument(
                "hello",
                new Size(100, 50),
                [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))])));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionConsent: consent.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.CanToggleTextExtraction.Should().BeTrue();
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        viewModel.TextExtractionTool.Text.Should().Be("hello");
        consentState.Should().Be(AiFeatureConsentState.Granted);
        consent.Verify(
            service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        textExtraction.Verify(
            service => service.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenConsented_ShouldRenderCurrentImageInMemoryAndShowRegions()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        var sourceImage = new ImageFile("original.png");
        byte[] renderedImageBytes = [1, 2, 3, 4];
        var renderedImage = new MemoryStream(renderedImageBytes);

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(renderedImage);
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.Is<TextExtractionRequest>(request => IsExpectedInMemoryRequest(
                    request,
                    renderedImageBytes,
                    new Size(100, 50))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new RecognizedTextDocument(
                "hello",
                new Size(100, 50),
                [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))])));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(sourceImage, CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        viewModel.TextExtractionRegions.Should().ContainSingle();
        viewModel.TextExtractionRegions[0].Bounds.Should().Be(new RectangleF(8, 8, 24, 9));
        viewModel.TextExtractionTool.Text.Should().Be("hello");
        renderedImage.CanRead.Should().BeFalse();
        exporter.Verify(service => service.RenderToStreamAsync(
            It.IsAny<IDrawable[]>(),
            It.Is<ImageCanvasRenderOptions>(options =>
                options.CanvasSize == new Size(100, 50) &&
                options.CropRect == new Rectangle(0, 0, 100, 50))),
            Times.Once);
        exporter.Verify(service => service.SaveImageAsync(
            It.IsAny<string>(),
            It.IsAny<IDrawable[]>(),
            It.IsAny<ImageCanvasRenderOptions>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenQrCodeIsDetected_ShowsNormalizedQrOverlayAndIncludesItsValue()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.IsAny<TextExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new RecognizedTextDocument(
                "https://example.com/qr",
                new Size(100, 50),
                [],
                [new RecognizedQrCodeRegion(
                    "https://example.com/qr",
                    new RectangleF(-5, 10, 40, 30))])));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.TextExtractionRegions.Should().BeEmpty();
        viewModel.TextExtractionQrCodes.Should().ContainSingle();
        viewModel.TextExtractionQrCodes[0].Bounds.Should().Be(new RectangleF(0, 10, 35, 30));
        viewModel.TextExtractionTool.Text.Should().Be("https://example.com/qr");
        viewModel.IsTextExtractionOverlayVisible.Should().BeTrue();
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenEditRevisionChanged_ShouldRunAgainWhenReopened()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        int extractionCount = 0;

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                extractionCount++;
                return TextExtractionResult.Success(new RecognizedTextDocument(
                    "hello",
                    new Size(100, 50),
                    [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))]));
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);
        extractionCount.Should().Be(1);

        viewModel.AddDrawable(new RectangleDrawable(
            new Vector2(1, 1),
            new Size(10, 10),
            Color.Red,
            Color.Transparent,
            1));

        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.TextExtractionRegions.Should().BeEmpty();

        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        extractionCount.Should().Be(2);
        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        exporter.Verify(service => service.SaveImageAsync(
            It.IsAny<string>(),
            It.IsAny<IDrawable[]>(),
            It.IsAny<ImageCanvasRenderOptions>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenRepeatedlyOpenedWithoutEdits_ShouldReuseCachedResult()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.IsAny<TextExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new RecognizedTextDocument(
                "hello",
                new Size(100, 50),
                [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))], [])));
        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        for (int iteration = 0; iteration < 3; iteration++)
        {
            await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);
            await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);
        }

        viewModel.TextExtractionRegions.Should().ContainSingle();
        viewModel.TextExtractionTool.Text.Should().Be("hello");
        exporter.Verify(service => service.RenderToStreamAsync(
            It.IsAny<IDrawable[]>(),
            It.IsAny<ImageCanvasRenderOptions>()), Times.Once);
        textExtraction.Verify(service => service.ExtractAsync(
            It.IsAny<TextExtractionRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        exporter.Verify(service => service.SaveImageAsync(
            It.IsAny<string>(),
            It.IsAny<IDrawable[]>(),
            It.IsAny<ImageCanvasRenderOptions>()), Times.Never);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenModelPreparationIsRunning_ShouldRemainEnabledAndCancelOnSecondClick()
    {
        var textExtraction = new Mock<ITextExtractionService>();
        var pendingPreparation = new TaskCompletionSource<TextExtractionPreparationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken preparationToken = default;

        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.PreparationNeeded);
        textExtraction
            .Setup(service => service.EnsureReadyAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) =>
            {
                preparationToken = cancellationToken;
                return pendingPreparation.Task;
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        Task originalExecution = viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        viewModel.IsTextExtractionRunning.Should().BeTrue();
        viewModel.CanToggleTextExtraction.Should().BeTrue();
        viewModel.ToggleTextExtractionModeCommand.CanExecute(null).Should().BeTrue();

        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        preparationToken.IsCancellationRequested.Should().BeTrue();
        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.IsTextExtractionRunning.Should().BeFalse();
        viewModel.ToggleTextExtractionModeCommand.CanExecute(null).Should().BeTrue();

        pendingPreparation.SetResult(TextExtractionPreparationResult.Success);
        await originalExecution;

        textExtraction.Verify(service => service.ExtractAsync(
            It.IsAny<TextExtractionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ChangingModes_WhenTextExtractionIsRunning_ShouldCancelAndDismissProgress()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        var pendingResult = new TaskCompletionSource<TextExtractionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken extractionToken = default;

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.IsAny<TextExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns((TextExtractionRequest _, CancellationToken cancellationToken) =>
            {
                extractionToken = cancellationToken;
                return pendingResult.Task;
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        Task originalExecution = viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        viewModel.IsTextExtractionRunning.Should().BeTrue();

        viewModel.ToggleCropModeCommand.Execute(null);

        extractionToken.IsCancellationRequested.Should().BeTrue();
        viewModel.IsCropModeActive.Should().BeTrue();
        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.IsTextExtractionRunning.Should().BeFalse();

        pendingResult.SetResult(TextExtractionResult.Success(new RecognizedTextDocument(
            "stale",
            new Size(100, 50),
            [new RecognizedTextRegion("stale", new RectangleF(10, 10, 20, 5))])));
        await originalExecution;

        viewModel.TextExtractionRegions.Should().BeEmpty();
        viewModel.TextExtractionTool.Text.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenImageIsTooLarge_ShouldShowLocalizedMessage()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        var localization = new Mock<ILocalizationService>();
        var notifications = new Mock<IAppNotificationService>();

        exporter
            .Setup(service => service.RenderToStreamAsync(
                It.IsAny<IDrawable[]>(),
                It.IsAny<ImageCanvasRenderOptions>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.IsAny<TextExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.TooLarge());
        localization
            .Setup(service => service.GetString("TextExtractionStatus_TooLarge"))
            .Returns("Localized image-too-large message.");

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            textExtractionService: textExtraction.Object,
            localizationService: localization.Object,
            notificationService: notifications.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.TextExtractionStatusMessage.Should().Be("Localized image-too-large message.");
        notifications.Verify(
            service => service.ShowError("Localized image-too-large message."),
            Times.Once);
    }

    private static bool IsExpectedInMemoryRequest(
        TextExtractionRequest request,
        byte[] expectedBytes,
        Size expectedSize)
    {
        return request.SourceImage is MemoryStream stream &&
            stream.ToArray().SequenceEqual(expectedBytes) &&
            request.SourceSize == expectedSize;
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageCanvasExporter? imageCanvasExporter = null,
        IStorageService? storageService = null,
        ITextExtractionService? textExtractionService = null,
        ITextExtractionFeatureAvailability? textExtractionFeatureAvailability = null,
        IAiFeatureConsentService? textExtractionConsent = null,
        IClipboardService? clipboardService = null,
        ILocalizationService? localizationService = null,
        IAppNotificationService? notificationService = null,
        IAiFeatureConsentService? sharedConsent = null)
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(service => service.GetImageFileSize(It.IsAny<ImageFile>()))
            .Returns(new Size(100, 50));

        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());

        IAppNotificationService notifications = notificationService ?? Mock.Of<IAppNotificationService>();
        ILocalizationService localization = localizationService ?? Mock.Of<ILocalizationService>();

        return new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            imageCanvasExporter ?? Mock.Of<IImageCanvasExporter>(),
            Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == false),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            storageService ?? Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            clipboardService ?? Mock.Of<IClipboardService>(),
            new ColorPickerToolViewModel(
                Mock.Of<IClipboardService>(),
                localization,
                notifications),
            new ChromaKeyToolViewModel(
                Mock.Of<IChromaKeyAccessService>(service => service.IsChromaKeyEnabled == false),
                Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(
                clipboardService ?? Mock.Of<IClipboardService>(),
                localization,
                notifications),
            sharedConsent ?? textExtractionConsent ?? Mock.Of<IAiFeatureConsentService>(
                service => service.GetConsentState(AiFeatureId.TextExtraction) == AiFeatureConsentState.Granted &&
                service.EnsureConsentAsync(It.IsAny<CancellationToken>()) == Task.FromResult(true)),
            textExtractionService ?? Mock.Of<ITextExtractionService>(service => service.GetReadyState() == TextExtractionReadyState.Ready),
            textExtractionFeatureAvailability ?? Mock.Of<ITextExtractionFeatureAvailability>(service => service.IsTextExtractionEnabled == true));
    }
}
