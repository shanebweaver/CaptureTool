using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.AudioCapture;
using CaptureTool.Domain.Audio.Interfaces;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.AudioCapture;

[TestClass]
public class AudioCapturePlayUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldCallPlayOnService()
    {
        // Arrange
        var service = Fixture.Freeze<Mock<IAudioCaptureService>>();
        var useCase = Fixture.Create<AudioCapturePlayUseCase>();

        // Act
        useCase.Execute();

        // Assert
        service.Verify(s => s.Play(), Times.Once);
    }
}
