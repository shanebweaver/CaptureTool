using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Presentation.Features.VideoEdit;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public class VideoEditPageViewModelTrimTests
{
    [TestMethod]
    public void UpdateTrimEnd_ShouldMovePlayhead_WhenRangeShrinksBeforePlayhead()
    {
        var viewModel = CreateViewModel();
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdatePlayhead(8);

        viewModel.UpdateTrimEnd(5);

        Assert.AreEqual(5, viewModel.PlayheadSeconds);
    }

    [TestMethod]
    public void UpdateTrimStart_ShouldMovePlayhead_WhenRangeShrinksAfterPlayhead()
    {
        var viewModel = CreateViewModel();
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdatePlayhead(1);

        viewModel.UpdateTrimStart(3);

        Assert.AreEqual(3, viewModel.PlayheadSeconds);
    }

    [TestMethod]
    public void UpdatePlayhead_ShouldClampToTrimRange()
    {
        var viewModel = CreateViewModel();
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(7);

        viewModel.UpdatePlayhead(0);
        Assert.AreEqual(2, viewModel.PlayheadSeconds);

        viewModel.UpdatePlayhead(9);
        Assert.AreEqual(7, viewModel.PlayheadSeconds);
    }

    [TestMethod]
    public void ToggleTrimModeCommand_ShouldToggleTrimMode()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleTrimModeCommand.Execute(null);

        Assert.IsTrue(viewModel.IsInTrimMode);
    }

    [TestMethod]
    public async Task SaveCommand_ShouldIncludeTrimRange_WhenVideoIsTrimmed()
    {
        var trimmer = new Mock<IVideoFileTrimmer>();
        var viewModel = CreateViewModel(saveAction: CreateSaveUseCase(trimmer));
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(7);

        await viewModel.SaveCommand.ExecuteAsync(null);

        trimmer.Verify(
            service => service.TrimAsync(
                "test.mp4",
                It.IsAny<string>(),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(7),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CopyCommand_ShouldIncludeTrimRange_WhenVideoIsTrimmed()
    {
        var trimmer = new Mock<IVideoFileTrimmer>();
        var viewModel = CreateViewModel(copyAction: CreateCopyUseCase(trimmer));
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(1);
        viewModel.UpdateTrimEnd(6);

        await viewModel.CopyCommand.ExecuteAsync(null);

        trimmer.Verify(
            service => service.TrimAsync(
                "test.mp4",
                It.IsAny<string>(),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(6),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SaveCommand_ShouldNotIncludeTrimRange_WhenTrimRestoredToFullDuration()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        var destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        await File.WriteAllTextAsync(sourcePath, "video", TestContext.CancellationToken);

        var trimmer = new Mock<IVideoFileTrimmer>();
        var viewModel = CreateViewModel(saveAction: CreateSaveUseCase(trimmer, destinationPath));
        viewModel.Load(new VideoFile(sourcePath));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(7);
        viewModel.UpdateTrimStart(0);
        viewModel.UpdateTrimEnd(10);

        await viewModel.SaveCommand.ExecuteAsync(null);

        trimmer.Verify(
            service => service.TrimAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        File.Delete(sourcePath);
        File.Delete(destinationPath);
    }

    [TestMethod]
    public void UpdateTrimRange_ShouldMarkSessionDirty()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));

        viewModel.UpdateTrimStart(2);

        Assert.IsTrue(viewModel.HasUnsavedChanges);
    }

    [TestMethod]
    public void UpdateTrimRange_ShouldAutoSaveTrimState_WhenEnabled()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureTool.Application.Features.Settings.CaptureToolSettings.Settings_Edit_AutoSave))
            .Returns(true);
        var stateStore = new Mock<IEditSessionStateStore>();
        stateStore
            .Setup(service => service.SaveVideoTrimStateAsync(It.IsAny<string>(), It.IsAny<VideoTrimState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        stateStore
            .Setup(service => service.TryReadVideoTrimStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoTrimState?)null);
        var viewModel = CreateViewModel(settingsService: settings.Object, editSessionStateStore: stateStore.Object);
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));

        viewModel.UpdateTrimStart(2);

        stateStore.Verify(
            service => service.SaveVideoTrimStateAsync(
                "test.mp4",
                It.Is<VideoTrimState>(state => state.DurationSeconds == 10 && state.TrimStartSeconds == 2 && state.TrimEndSeconds == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static VideoEditPageViewModel CreateViewModel(
        ISaveVideoFileUseCase? saveAction = null,
        ICopyVideoFileUseCase? copyAction = null,
        ISettingsService? settingsService = null,
        IEditSessionStateStore? editSessionStateStore = null,
        ILogService? logService = null)
    {
        return new(
            saveAction ?? CreateSaveUseCase(),
            copyAction ?? CreateCopyUseCase(),
            settingsService ?? Mock.Of<ISettingsService>(),
            editSessionStateStore ?? Mock.Of<IEditSessionStateStore>(),
            logService ?? Mock.Of<ILogService>());
    }

    private static ISaveVideoFileUseCase CreateSaveUseCase(
        Mock<IVideoFileTrimmer>? trimmer = null,
        string? destinationPath = null)
    {
        destinationPath ??= Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        var filePicker = new Mock<IFilePickerService>();
        filePicker
            .Setup(service => service.PickSaveFileAsync(FilePickerType.Video, UserFolder.Videos))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));

        var videoTrimmer = trimmer ?? new Mock<IVideoFileTrimmer>();
        var useCase = new Mock<ISaveVideoFileUseCase>();
        useCase
            .Setup(service => service.ExecuteAsync(It.IsAny<SaveVideoFileRequest>(), It.IsAny<CancellationToken>()))
            .Returns<SaveVideoFileRequest, CancellationToken>(async (request, cancellationToken) =>
            {
                if (request.TrimStart.HasValue && request.TrimEnd.HasValue)
                {
                    await videoTrimmer.Object.TrimAsync(request.VideoPath, destinationPath, request.TrimStart.Value, request.TrimEnd.Value, cancellationToken);
                }
                return UseCaseResponse<SaveVideoFileResponse>.Success(new SaveVideoFileResponse());
            });

        return useCase.Object;
    }

    private static ICopyVideoFileUseCase CreateCopyUseCase(Mock<IVideoFileTrimmer>? trimmer = null)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        storage.Setup(service => service.GetTemporaryFileName()).Returns($"{Guid.NewGuid()}.tmp");

        var videoTrimmer = trimmer ?? new Mock<IVideoFileTrimmer>();
        var useCase = new Mock<ICopyVideoFileUseCase>();
        useCase
            .Setup(service => service.ExecuteAsync(It.IsAny<CopyVideoFileRequest>(), It.IsAny<CancellationToken>()))
            .Returns<CopyVideoFileRequest, CancellationToken>(async (request, cancellationToken) =>
            {
                if (request.TrimStart.HasValue && request.TrimEnd.HasValue)
                {
                    string destinationPath = Path.Combine(storage.Object.GetApplicationTemporaryFolderPath(), storage.Object.GetTemporaryFileName());
                    await videoTrimmer.Object.TrimAsync(request.VideoPath, destinationPath, request.TrimStart.Value, request.TrimEnd.Value, cancellationToken);
                }
                return UseCaseResponse<CopyVideoFileResponse>.Success(new CopyVideoFileResponse());
            });

        return useCase.Object;
    }

    public TestContext TestContext { get; set; } = null!;
}
