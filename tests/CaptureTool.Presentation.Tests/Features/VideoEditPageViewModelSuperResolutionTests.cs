using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Files;
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
    [DataRow("consent", false)]
    [DataRow("preparation", false)]
    [DataRow("generation", false)]
    [DataRow("generation", true)]
    public async Task DisposeDuringEnhancementCancelsAndRejectsLateCompletion(string stage, bool fault)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var service = CreateReadyService("enhanced.mp4", stage == "preparation"
            ? VideoSuperResolutionReadyState.PreparationNeeded : VideoSuperResolutionReadyState.Ready);
        var consent = new Mock<IAiFeatureConsentService>();
        consent.Setup(s => s.GetConsentState(AiFeatureId.VideoSuperResolution)).Returns(stage == "consent"
            ? AiFeatureConsentState.Unknown : AiFeatureConsentState.Granted);
        consent.Setup(s => s.EnsureConsentAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) =>
        {
            observed = ct;
            await completion.Task;
            return true;
        });
        service.Setup(s => s.EnsureReadyAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) =>
        {
            observed = ct;
            await completion.Task;
            return VideoSuperResolutionPreparationResult.Success;
        });
        service.Setup(s => s.GenerateAsync(It.IsAny<VideoSuperResolutionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (VideoSuperResolutionRequest _, CancellationToken ct) =>
            {
                observed = ct;
                await completion.Task;
                return VideoSuperResolutionResult.Success(new VideoFile("enhanced.mp4"));
            });
        var scratch = new Mock<IScratchArtifactStore>();
        var vm = CreateViewModel(service, consentService: consent.Object, scratchArtifactStore: scratch.Object);
        vm.Load(new VideoFile("working.mp4", "retained.mp4"));
        Task command = vm.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
        Assert.IsTrue(observed.CanBeCanceled);
        vm.Dispose();
        Assert.IsTrue(observed.IsCancellationRequested);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        if (fault) completion.SetException(new InvalidOperationException("Late failure"));
        else completion.SetResult();
        await command.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.IsEmpty(changes, "Late completion must not update the disposed editor.");
        Assert.IsNull(vm.VideoPath);
        Assert.AreEqual(CaptureTool.Presentation.Loading.LoadState.Disposed, vm.LoadState);
        Assert.IsFalse(vm.IsVideoSuperResolutionActive);
        scratch.Verify(s => s.DeleteArtifact("enhanced.mp4"), stage == "generation" && !fault ? Times.Once() : Times.Never());
        scratch.Verify(s => s.DeleteArtifact("retained.mp4"), Times.Never);
        if (stage != "generation") service.Verify(s => s.GenerateAsync(It.IsAny<VideoSuperResolutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ConsentRevocationDiscardsLateGeneratedArtifact()
    {
        using var revoked = new CancellationTokenSource();
        var consent = new Mock<IAiFeatureConsentService>();
        consent.Setup(s => s.GetConsentState(AiFeatureId.VideoSuperResolution)).Returns(AiFeatureConsentState.Granted);
        consent.SetupGet(s => s.Revoked).Returns(revoked.Token);
        var completion = new TaskCompletionSource<VideoSuperResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var service = CreateReadyService("enhanced.mp4");
        service.Setup(s => s.GenerateAsync(It.IsAny<VideoSuperResolutionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((VideoSuperResolutionRequest _, CancellationToken ct) => { observed = ct; return completion.Task; });
        var scratch = new Mock<IScratchArtifactStore>();
        using var vm = CreateViewModel(service, consentService: consent.Object, scratchArtifactStore: scratch.Object);
        vm.Load(new VideoFile("working.mp4", "retained.mp4"));
        Task command = vm.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);
        revoked.Cancel();
        Assert.IsTrue(observed.IsCancellationRequested);
        completion.SetResult(VideoSuperResolutionResult.Success(new VideoFile("enhanced.mp4")));
        await command;
        Assert.AreEqual("working.mp4", vm.VideoPath);
        Assert.IsFalse(vm.IsVideoSuperResolutionActive);
        Assert.IsFalse(vm.IsVideoSuperResolutionGenerating);
        scratch.Verify(s => s.DeleteArtifact("enhanced.mp4"), Times.Once);
        scratch.Verify(s => s.DeleteArtifact("retained.mp4"), Times.Never);
    }

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
    public void Load_AfterPreviousEditorDisposed_ShouldRecreateWorkingCopy()
    {
        var existingFiles = new HashSet<string> { "scratch\\working.mp4", "recordings\\source.mp4" };
        var files = new Mock<IFileSystem>();
        files.Setup(service => service.FileExists(It.IsAny<string>()))
            .Returns<string>(existingFiles.Contains);
        var scratchArtifactStore = new Mock<IScratchArtifactStore>();
        scratchArtifactStore.Setup(store => store.DeleteArtifact(It.IsAny<string>()))
            .Callback<string>(path => existingFiles.Remove(path));
        scratchArtifactStore.Setup(store => store.CreateLeasedArtifactPath(It.IsAny<string>(), ".mp4"))
            .Returns("scratch\\restored.mp4");
        var navigationFile = new VideoFile("scratch\\working.mp4", "recordings\\source.mp4");
        var previous = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object, fileSystem: files.Object);
        previous.Load(navigationFile);
        files.Verify(service => service.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        previous.Dispose();

        var reopened = CreateViewModel(scratchArtifactStore: scratchArtifactStore.Object, fileSystem: files.Object);
        reopened.Load(navigationFile);

        reopened.VideoPath.Should().Be("scratch\\restored.mp4");
        reopened.DetailsSourcePath.Should().Be("recordings\\source.mp4");
        navigationFile.FilePath.Should().Be("scratch\\restored.mp4");
        files.Verify(service => service.CopyFile("recordings\\source.mp4", "scratch\\restored.mp4", true), Times.Once);
        reopened.Dispose();
        scratchArtifactStore.Verify(store => store.DeleteArtifact("scratch\\working.mp4"), Times.Once);
        scratchArtifactStore.Verify(store => store.DeleteArtifact("scratch\\restored.mp4"), Times.Once);
        scratchArtifactStore.Verify(store => store.DeleteArtifact("recordings\\source.mp4"), Times.Never);
    }

    [TestMethod]
    public async Task ToggleVideoSuperResolutionCommand_WhenConsentUnknown_ShouldRequestAndPersistConsent()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        consent
            .Setup(service => service.GetConsentState(AiFeatureId.VideoSuperResolution))
            .Returns(AiFeatureConsentState.Unknown);
        consent
            .Setup(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: CreateReadyService("enhanced.mp4"),
            consentService: consent.Object);

        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        consent.Verify(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        consent
            .Setup(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        VideoEditPageViewModel viewModel = CreateViewModel(
            service: service,
            consentService: consent.Object);

        viewModel.Load(new VideoFile("source.mp4"));

        await viewModel.ToggleVideoSuperResolutionCommand.ExecuteAsync(null);

        service.Verify(service => service.GenerateAsync(
            It.IsAny<VideoSuperResolutionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        consent.Verify(service => service.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        IScratchArtifactStore? scratchArtifactStore = null,
        IFileSystem? fileSystem = null)
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
            localization.Object,
            Mock.Of<IAppNotificationService>(),
            scratchArtifactStore: scratchArtifactStore,
            fileSystem: fileSystem);
    }

    public TestContext TestContext { get; set; } = null!;
}
