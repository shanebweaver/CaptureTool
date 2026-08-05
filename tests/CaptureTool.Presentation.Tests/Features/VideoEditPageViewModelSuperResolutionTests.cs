using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.VideoEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class VideoEditPageViewModelSuperResolutionTests
{
    [TestMethod]
    public void Load_WhenFeatureIsReady_ShouldExposeVideoSuperResolution()
    {
        VideoEditPageViewModel viewModel = CreateViewModel();

        viewModel.Load(new VideoFile("source.mp4"));

        viewModel.IsVideoSuperResolutionFeatureEnabled.Should().BeTrue();
        viewModel.IsVideoSuperResolutionAvailable.Should().BeTrue();
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_ShouldGenerateAndActivateEnhancedVideo()
    {
        var service = CreateReadyService("enhanced.mp4");
        var saveAction = new Mock<ISaveVideoFileUseCase>();
        saveAction
            .Setup(action => action.ExecuteAsync(
                It.IsAny<SaveVideoFileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SaveVideoFileResponse>.Success(new SaveVideoFileResponse()));
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: service,
            saveAction: saveAction.Object);
        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
        viewModel.VideoPath.Should().Be("enhanced.mp4");
        saveAction.Verify(action => action.ExecuteAsync(
            It.Is<SaveVideoFileRequest>(request => request.VideoPath == "enhanced.mp4"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Dispose_ShouldReleaseWorkingCopyAndGeneratedDerivative()
    {
        var scratchArtifactStore = new Mock<IScratchArtifactStore>();
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: CreateReadyService("enhanced.mp4"),
            scratchArtifactStore: scratchArtifactStore.Object);
        viewModel.Load(new VideoFile("working.mp4"));
        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        viewModel.Dispose();

        scratchArtifactStore.Verify(store => store.DeleteArtifact("working.mp4"), Times.Once);
        scratchArtifactStore.Verify(store => store.DeleteArtifact("enhanced.mp4"), Times.Once);
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_WhenConsentUnknown_ShouldRequestAndPersistConsent()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        consent
            .Setup(service => service.GetConsentState(AiFeatureId.VideoSuperResolution))
            .Returns(AiFeatureConsentState.Unknown);
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        dialog
            .Setup(service => service.RequestConsentAsync(
                AiFeatureId.VideoSuperResolution,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: CreateReadyService("enhanced.mp4"),
            consentService: consent.Object,
            consentDialogService: dialog.Object);
        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        dialog.Verify(service => service.RequestConsentAsync(
            AiFeatureId.VideoSuperResolution,
            It.IsAny<CancellationToken>()), Times.Once);
        consent.Verify(service => service.SetConsentAsync(
            AiFeatureId.VideoSuperResolution,
            true,
            It.IsAny<CancellationToken>()), Times.Once);
        viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_WhenConsentDeclined_ShouldNotGenerate()
    {
        var service = CreateReadyService("enhanced.mp4");
        var consent = new Mock<IAiFeatureConsentService>();
        consent
            .Setup(service => service.GetConsentState(AiFeatureId.VideoSuperResolution))
            .Returns(AiFeatureConsentState.Unknown);
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        dialog
            .Setup(service => service.RequestConsentAsync(
                AiFeatureId.VideoSuperResolution,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: service,
            consentService: consent.Object,
            consentDialogService: dialog.Object);
        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        service.Verify(service => service.GenerateAsync(
            It.IsAny<VideoSuperResolutionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        consent.Verify(service => service.SetConsentAsync(
            AiFeatureId.VideoSuperResolution,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
        viewModel.IsVideoSuperResolutionActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_ShouldPrepareWhenNeeded()
    {
        var service = CreateReadyService(
            "enhanced.mp4",
            VideoSuperResolutionReadyState.PreparationNeeded);
        service
            .Setup(service => service.EnsureReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(VideoSuperResolutionPreparationResult.Success);
        VideoEditPageViewModel viewModel = CreateViewModel(service: service);
        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        service.Verify(service => service.EnsureReadyAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_ShouldReuseCachedEnhancedVideo()
    {
        string enhancedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(enhancedPath, [0], TestContext.CancellationToken);
        try
        {
            var service = CreateReadyService(enhancedPath);
            VideoEditPageViewModel viewModel = CreateViewModel(service: service);
            viewModel.Load(new VideoFile("source.mp4"));

            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

            service.Verify(service => service.GenerateAsync(
                It.IsAny<VideoSuperResolutionRequest>(),
                It.IsAny<CancellationToken>()), Times.Once);
            viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
            viewModel.VideoPath.Should().Be(enhancedPath);
        }
        finally
        {
            File.Delete(enhancedPath);
        }
    }

    [TestMethod]
    public async Task EnhancedVideoDurationLoad_ShouldPreserveExistingTrimRange()
    {
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: CreateReadyService("enhanced.mp4"));
        viewModel.Load(new VideoFile("source.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(8);

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));

        viewModel.TrimStartSeconds.Should().Be(2);
        viewModel.TrimEndSeconds.Should().Be(8);
    }

    [TestMethod]
    public async Task SaveEnhancedVariant_ThenSwitchAwayAndBack_ShouldCompareWithSavedVariant()
    {
        string enhancedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(enhancedPath, [0], TestContext.CancellationToken);
        try
        {
            VideoEditPageViewModel viewModel = CreateViewModel(
                service: CreateReadyService(enhancedPath),
                saveAction: CreateSuccessfulSaveAction().Object);
            viewModel.Load(new VideoFile("source.mp4"));
            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

            (await viewModel.SaveAsync(TestContext.CancellationToken)).Should().BeTrue();
            viewModel.HasUnsavedChanges.Should().BeFalse();

            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            viewModel.IsVideoSuperResolutionActive.Should().BeFalse();
            viewModel.HasUnsavedChanges.Should().BeTrue();

            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
            viewModel.HasUnsavedChanges.Should().BeFalse();
        }
        finally
        {
            File.Delete(enhancedPath);
        }
    }

    [TestMethod]
    public async Task SaveOriginalVariant_WithEnhancedVideoCached_ShouldCompareWithSavedVariant()
    {
        string enhancedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(enhancedPath, [0], TestContext.CancellationToken);
        try
        {
            VideoEditPageViewModel viewModel = CreateViewModel(
                service: CreateReadyService(enhancedPath),
                saveAction: CreateSuccessfulSaveAction().Object);
            viewModel.Load(new VideoFile("source.mp4"));
            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

            (await viewModel.SaveAsync(TestContext.CancellationToken)).Should().BeTrue();
            viewModel.IsVideoSuperResolutionActive.Should().BeFalse();
            viewModel.HasUnsavedChanges.Should().BeFalse();

            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            viewModel.IsVideoSuperResolutionActive.Should().BeTrue();
            viewModel.HasUnsavedChanges.Should().BeTrue();

            await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
            viewModel.IsVideoSuperResolutionActive.Should().BeFalse();
            viewModel.HasUnsavedChanges.Should().BeFalse();
        }
        finally
        {
            File.Delete(enhancedPath);
        }
    }

    private static Mock<IVideoSuperResolutionService> CreateReadyService(
        string outputPath,
        VideoSuperResolutionReadyState readyState = VideoSuperResolutionReadyState.Ready)
    {
        var service = new Mock<IVideoSuperResolutionService>();
        service
            .Setup(service => service.GetReadyState())
            .Returns(readyState);
        service
            .Setup(service => service.GenerateAsync(
                It.IsAny<VideoSuperResolutionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(VideoSuperResolutionResult.Success(new VideoFile(outputPath)));
        return service;
    }

    private static Mock<ISaveVideoFileUseCase> CreateSuccessfulSaveAction()
    {
        var saveAction = new Mock<ISaveVideoFileUseCase>();
        saveAction
            .Setup(action => action.ExecuteAsync(
                It.IsAny<SaveVideoFileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SaveVideoFileResponse>.Success(new SaveVideoFileResponse()));
        return saveAction;
    }

    private static VideoEditPageViewModel CreateViewModel(
        Mock<IVideoSuperResolutionService>? service = null,
        ISaveVideoFileUseCase? saveAction = null,
        IAiFeatureConsentService? consentService = null,
        IAiFeatureConsentDialogService? consentDialogService = null,
        IScratchArtifactStore? scratchArtifactStore = null)
    {
        service ??= CreateReadyService("enhanced.mp4");
        consentService ??= Mock.Of<IAiFeatureConsentService>(consent =>
            consent.GetConsentState(AiFeatureId.VideoSuperResolution) == AiFeatureConsentState.Granted);

        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns<string>(resourceKey => resourceKey);

        return new VideoEditPageViewModel(
            saveAction ?? Mock.Of<ISaveVideoFileUseCase>(),
            Mock.Of<ICopyVideoFileUseCase>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IOpenVideosFolderUseCase>(),
            Mock.Of<ILogService>(),
            service.Object,
            Mock.Of<IVideoSuperResolutionFeatureAvailability>(
                availability => availability.IsVideoSuperResolutionEnabled),
            consentService,
            consentDialogService ?? Mock.Of<IAiFeatureConsentDialogService>(),
            localization.Object,
            Mock.Of<IAppNotificationService>(),
            scratchArtifactStore: scratchArtifactStore);
    }

    public TestContext TestContext { get; set; } = null!;
}
