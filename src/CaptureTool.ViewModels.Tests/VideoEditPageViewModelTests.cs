using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Domains.Capture.Interfaces;
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

        Fixture.Freeze<Mock<IVideoEditSaveAction>>();
        Fixture.Freeze<Mock<IVideoEditCopyAction>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public async Task SaveCommand_ShouldInvokeSaveAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var saveAction = Fixture.Freeze<Mock<IVideoEditSaveAction>>();
        var vm = Create();
        
        // Set up a video file
        var videoFile = new VideoFile("test.mp4");
        vm.Load(videoFile);

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        saveAction.Verify(a => a.ExecuteAsync("test.mp4", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(VideoEditPageViewModel.ActivityIds.Save), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(VideoEditPageViewModel.ActivityIds.Save), Times.Once);
    }

    [TestMethod]
    public async Task CopyCommand_ShouldInvokeCopyAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var copyAction = Fixture.Freeze<Mock<IVideoEditCopyAction>>();
        var vm = Create();
        
        // Set up a video file
        var videoFile = new VideoFile("test.mp4");
        vm.Load(videoFile);

        // Act
        await vm.CopyCommand.ExecuteAsync(null);

        // Assert
        copyAction.Verify(a => a.ExecuteAsync("test.mp4", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(VideoEditPageViewModel.ActivityIds.Copy), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(VideoEditPageViewModel.ActivityIds.Copy), Times.Once);
    }

    [TestMethod]
    public void Load_WithReadyVideo_ShouldSetIsVideoReadyTrue()
    {
        // Arrange
        var vm = Create();
        var videoFile = new VideoFile("test.mp4");

        // Act
        vm.Load(videoFile);

        // Assert
        Assert.IsTrue(vm.IsVideoReady);
        Assert.IsFalse(vm.IsFinalizingVideo);
    }

    [TestMethod]
    public void Load_WithPendingVideo_ShouldSetIsFinalizingVideoTrue()
    {
        // Arrange
        var vm = Create();
        var pendingVideo = new PendingVideoFile("test.mp4");

        // Act
        vm.Load(pendingVideo);

        // Assert
        Assert.IsFalse(vm.IsVideoReady);
        Assert.IsTrue(vm.IsFinalizingVideo);
    }

    [TestMethod]
    public async Task Load_WithPendingVideo_ShouldSetIsVideoReadyAfterCompletion()
    {
        // Arrange
        var vm = Create();
        var pendingVideo = new PendingVideoFile("test.mp4");

        // Act
        vm.Load(pendingVideo);
        
        // Simulate completion
        pendingVideo.Complete();
        await Task.Delay(100); // Give time for async handler

        // Assert
        Assert.IsTrue(vm.IsVideoReady);
        Assert.IsFalse(vm.IsFinalizingVideo);
    }
}
