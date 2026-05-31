using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
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

    private static VideoEditPageViewModel CreateViewModel()
    {
        return new(
            Mock.Of<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>>(),
            Mock.Of<IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>>(),
            Mock.Of<ITelemetryService>());
    }
}
