using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.UseCases.AudioEdit;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public class AudioEditPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private AudioEditPageViewModel Create() => Fixture.Create<AudioEditPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IAudioEditSaveUseCase>>();
        Fixture.Freeze<Mock<IAudioEditCopyUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public async Task SaveCommand_ShouldInvokeSaveAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var saveAction = Fixture.Freeze<Mock<IAudioEditSaveUseCase>>();
        var vm = Create();

        // Set up an audio file
        var audioFile = new AudioFile("test.mp3");
        vm.Load(audioFile);

        // Act
        await vm.SaveCommand.ExecuteAsync();

        // Assert
        saveAction.Verify(a => a.ExecuteAsync("test.mp3", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioEditPageViewModel.ActivityIds.Save, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioEditPageViewModel.ActivityIds.Save, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task CopyCommand_ShouldInvokeCopyAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var copyAction = Fixture.Freeze<Mock<IAudioEditCopyUseCase>>();
        var vm = Create();

        // Set up an audio file
        var audioFile = new AudioFile("test.mp3");
        vm.Load(audioFile);

        // Act
        await vm.CopyCommand.ExecuteAsync();

        // Assert
        copyAction.Verify(a => a.ExecuteAsync("test.mp3", It.IsAny<CancellationToken>()), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioEditPageViewModel.ActivityIds.Copy, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioEditPageViewModel.ActivityIds.Copy, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void Load_WithAudioFile_ShouldSetIsAudioReadyTrue()
    {
        // Arrange
        var vm = Create();
        var audioFile = new AudioFile("test.mp3");

        // Act
        vm.Load(audioFile);

        // Assert
        Assert.IsTrue(vm.IsAudioReady);
        Assert.AreEqual("test.mp3", vm.AudioPath);
    }

    [TestMethod]
    public void Load_WithWavFile_ShouldSetProperties()
    {
        // Arrange
        var vm = Create();
        var audioFile = new AudioFile("test.wav");

        // Act
        vm.Load(audioFile);

        // Assert
        Assert.IsTrue(vm.IsAudioReady);
        Assert.AreEqual("test.wav", vm.AudioPath);
    }

    [TestMethod]
    public void Load_WithFlacFile_ShouldSetProperties()
    {
        // Arrange
        var vm = Create();
        var audioFile = new AudioFile("test.flac");

        // Act
        vm.Load(audioFile);

        // Assert
        Assert.IsTrue(vm.IsAudioReady);
        Assert.AreEqual("test.flac", vm.AudioPath);
    }
}
