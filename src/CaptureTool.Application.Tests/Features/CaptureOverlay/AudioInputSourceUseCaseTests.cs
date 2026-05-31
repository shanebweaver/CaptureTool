using CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Infrastructure.Abstractions.Audio;
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

        GetAudioInputSourcesUseCase useCase = new(service.Object);

        // Act
        GetAudioInputSourcesResponse response = await useCase.ExecuteAsync(new GetAudioInputSourcesRequest());

        // Assert
        response.Sources.Should().BeEquivalentTo(sources);
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

        SelectAudioInputSourceUseCase useCase = new(service.Object);

        // Act
        SelectAudioInputSourceResponse response = await useCase.ExecuteAsync(new SelectAudioInputSourceRequest("default"));

        // Assert
        response.IsAvailable.Should().BeTrue();
        response.WasRemoved.Should().BeFalse();
    }

    [TestMethod]
    public async Task SelectAudioInputSource_ShouldReportRemovedSource()
    {
        // Arrange
        Mock<IAudioInputDetectionService> service = new();
        service
            .Setup(x => x.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        SelectAudioInputSourceUseCase useCase = new(service.Object);

        // Act
        SelectAudioInputSourceResponse response = await useCase.ExecuteAsync(new SelectAudioInputSourceRequest("missing"));

        // Assert
        response.IsAvailable.Should().BeFalse();
        response.WasRemoved.Should().BeTrue();
    }
}
