using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
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
        var saveAction = new Mock<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>>();
        var viewModel = CreateViewModel(saveAction: saveAction);
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(7);

        await viewModel.SaveCommand.ExecuteAsync(null);

        saveAction.Verify(
            action => action.ExecuteAsync(
                It.Is<SaveVideoFileRequest>(request =>
                    request.VideoPath == "test.mp4" &&
                    request.TrimStart == TimeSpan.FromSeconds(2) &&
                    request.TrimEnd == TimeSpan.FromSeconds(7)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CopyCommand_ShouldIncludeTrimRange_WhenVideoIsTrimmed()
    {
        var copyAction = new Mock<IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>>();
        var viewModel = CreateViewModel(copyAction: copyAction);
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(1);
        viewModel.UpdateTrimEnd(6);

        await viewModel.CopyCommand.ExecuteAsync(null);

        copyAction.Verify(
            action => action.ExecuteAsync(
                It.Is<CopyVideoFileRequest>(request =>
                    request.VideoPath == "test.mp4" &&
                    request.TrimStart == TimeSpan.FromSeconds(1) &&
                    request.TrimEnd == TimeSpan.FromSeconds(6)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SaveCommand_ShouldNotIncludeTrimRange_WhenTrimRestoredToFullDuration()
    {
        var saveAction = new Mock<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>>();
        var viewModel = CreateViewModel(saveAction: saveAction);
        viewModel.Load(new VideoFile("test.mp4"));
        viewModel.SetVideoDuration(TimeSpan.FromSeconds(10));
        viewModel.UpdateTrimStart(2);
        viewModel.UpdateTrimEnd(7);
        viewModel.UpdateTrimStart(0);
        viewModel.UpdateTrimEnd(10);

        await viewModel.SaveCommand.ExecuteAsync(null);

        saveAction.Verify(
            action => action.ExecuteAsync(
                It.Is<SaveVideoFileRequest>(request =>
                    request.VideoPath == "test.mp4" &&
                    request.TrimStart == null &&
                    request.TrimEnd == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static VideoEditPageViewModel CreateViewModel(
        Mock<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>>? saveAction = null,
        Mock<IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>>? copyAction = null)
    {
        return new(
            (saveAction ?? new Mock<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>>()).Object,
            (copyAction ?? new Mock<IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>>()).Object,
            Mock.Of<ITelemetryService>());
    }
}
