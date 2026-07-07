using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.Features.CaptureOverlay;

[TestClass]
public sealed class AudioInputSourceUseCaseTests
{
    [TestMethod]
    public async Task GetAudioInputSources_ShouldReturnSourcesFromDetectionService()
    {
        // Arrange
        AudioInputSource[] sources =
        [
            new("default", "Default microphone", true),
            new("external", "External microphone", false)
        ];

        Mock<IAudioInputDetectionService> service = new();
        service
            .Setup(x => x.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        GetAudioInputSourcesUseCase useCase = new(service.Object, TestUseCaseExecutor.Instance);

        // Act
        GetAudioInputSourcesResponse? response = (await useCase.ExecuteAsync(new GetAudioInputSourcesRequest(), TestContext.CancellationToken)).Value;

        // Assert
        response.Should().NotBeNull();
        response!.Sources.Should().BeEquivalentTo(sources);
    }

    [TestMethod]
    public async Task SelectAudioInputSource_ShouldReportAvailableSource()
    {
        // Arrange
        AudioInputSource[] sources =
        [
            new("default", "Default microphone", true)
        ];

        Mock<IAudioInputDetectionService> service = new();
        service
            .Setup(x => x.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        var videoCaptureWorkflow = new FakeVideoCaptureWorkflow();
        SelectAudioInputSourceUseCase useCase = new(service.Object, videoCaptureWorkflow, TestUseCaseExecutor.Instance);

        // Act
        SelectAudioInputSourceResponse? response = (await useCase.ExecuteAsync(new SelectAudioInputSourceRequest("default"), TestContext.CancellationToken)).Value;

        // Assert
        response.Should().NotBeNull();
        response!.IsAvailable.Should().BeTrue();
        response.WasRemoved.Should().BeFalse();
        videoCaptureWorkflow.LastSelectedAudioInputSourceId.Should().Be("default");
    }

    [TestMethod]
    public async Task SelectAudioInputSource_ShouldReportRemovedSource()
    {
        // Arrange
        Mock<IAudioInputDetectionService> service = new();
        service
            .Setup(x => x.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var videoCaptureWorkflow = new FakeVideoCaptureWorkflow();
        SelectAudioInputSourceUseCase useCase = new(service.Object, videoCaptureWorkflow, TestUseCaseExecutor.Instance);

        // Act
        SelectAudioInputSourceResponse? response = (await useCase.ExecuteAsync(new SelectAudioInputSourceRequest("missing"), TestContext.CancellationToken)).Value;

        // Assert
        response.Should().NotBeNull();
        response!.IsAvailable.Should().BeFalse();
        response.WasRemoved.Should().BeTrue();
        videoCaptureWorkflow.SelectAudioInputSourceWasCalled.Should().BeFalse();
    }

    [TestMethod]
    public async Task SelectAudioInputSource_ShouldClearSelectedSource_WhenSourceIdIsBlank()
    {
        Mock<IAudioInputDetectionService> service = new();
        var videoCaptureWorkflow = new FakeVideoCaptureWorkflow();
        SelectAudioInputSourceUseCase useCase = new(service.Object, videoCaptureWorkflow, TestUseCaseExecutor.Instance);

        SelectAudioInputSourceResponse? response = (await useCase.ExecuteAsync(new SelectAudioInputSourceRequest(null), TestContext.CancellationToken)).Value;

        response.Should().NotBeNull();
        response!.IsAvailable.Should().BeFalse();
        response.WasRemoved.Should().BeFalse();
        videoCaptureWorkflow.SelectAudioInputSourceWasCalled.Should().BeTrue();
        videoCaptureWorkflow.LastSelectedAudioInputSourceId.Should().BeNull();
        service.Verify(x => x.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    public TestContext TestContext { get; set; } = null!;
}
