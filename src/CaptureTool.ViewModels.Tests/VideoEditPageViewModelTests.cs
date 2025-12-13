using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Services.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public class VideoEditPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private VideoEditPageViewModel Create() => Fixture.Create<VideoEditPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IVideoEditActions>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public async Task SaveCommand_ShouldInvokeVideoEditActions_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var videoEditActions = Fixture.Freeze<Mock<IVideoEditActions>>();
        var vm = Create();
        
        // Set up a video path
        var videoFile = new Domains.Capture.Interfaces.VideoFile("test.mp4");
        vm.Load(videoFile);

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        videoEditActions.Verify(a => a.SaveAsync("test.mp4", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(VideoEditPageViewModel.ActivityIds.Save), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(VideoEditPageViewModel.ActivityIds.Save), Times.Once);
    }

    [TestMethod]
    public async Task CopyCommand_ShouldInvokeVideoEditActions_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var videoEditActions = Fixture.Freeze<Mock<IVideoEditActions>>();
        var vm = Create();
        
        // Set up a video path
        var videoFile = new Domains.Capture.Interfaces.VideoFile("test.mp4");
        vm.Load(videoFile);

        // Act
        await vm.CopyCommand.ExecuteAsync(null);

        // Assert
        videoEditActions.Verify(a => a.CopyAsync("test.mp4", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(VideoEditPageViewModel.ActivityIds.Copy), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(VideoEditPageViewModel.ActivityIds.Copy), Times.Once);
    }
}
