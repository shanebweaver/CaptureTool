using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Interfaces;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.AudioCapture;

[TestClass]
public class AudioCaptureMuteUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldCallToggleMuteOnService()
    {
        // Arrange
        var service = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var useCase = Fixture.Create<AudioCaptureMuteUseCase>();

        // Act
        useCase.Execute();

        // Assert
        service.Verify(s => s.ToggleMute(), Times.Once);
    }
}
