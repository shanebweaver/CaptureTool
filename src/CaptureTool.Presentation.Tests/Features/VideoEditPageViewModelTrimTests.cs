using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Clipboard;
using CaptureTool.Infrastructure.Abstractions.Media;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Abstractions.Windowing;
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
        await File.WriteAllTextAsync(sourcePath, "video");

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

    private static VideoEditPageViewModel CreateViewModel(
        SaveVideoFileUseCase? saveAction = null,
        CopyVideoFileUseCase? copyAction = null)
    {
        return new(
            saveAction ?? CreateSaveUseCase(),
            copyAction ?? CreateCopyUseCase(),
            Mock.Of<ITelemetryService>());
    }

    private static SaveVideoFileUseCase CreateSaveUseCase(
        Mock<IVideoFileTrimmer>? trimmer = null,
        string? destinationPath = null)
    {
        destinationPath ??= Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        var filePicker = new Mock<IFilePickerService>();
        filePicker
            .Setup(service => service.PickSaveFileAsync(It.IsAny<nint>(), FilePickerType.Video, UserFolder.Videos))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == destinationPath));

        return new SaveVideoFileUseCase(
            filePicker.Object,
            Mock.Of<IWindowHandleProvider>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IFeatureManager>(),
            (trimmer ?? new Mock<IVideoFileTrimmer>()).Object);
    }

    private static CopyVideoFileUseCase CreateCopyUseCase(Mock<IVideoFileTrimmer>? trimmer = null)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        storage.Setup(service => service.GetTemporaryFileName()).Returns($"{Guid.NewGuid()}.tmp");

        return new CopyVideoFileUseCase(
            Mock.Of<IClipboardService>(),
            storage.Object,
            (trimmer ?? new Mock<IVideoFileTrimmer>()).Object);
    }
}
