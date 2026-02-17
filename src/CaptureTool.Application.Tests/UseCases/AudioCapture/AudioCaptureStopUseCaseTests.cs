using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Interfaces;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.AudioCapture;

[TestClass]
public class AudioCaptureStopUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldCallStopOnService()
    {
        // Arrange
        var service = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var useCase = Fixture.Create<AudioCaptureStopUseCase>();

        // Act
        useCase.Execute();

        // Assert
        service.Verify(s => s.StopCapture(), Times.Once);
    }
}
