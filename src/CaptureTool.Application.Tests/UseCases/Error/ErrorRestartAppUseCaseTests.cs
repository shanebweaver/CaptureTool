using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.Error;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Error;

[TestClass]
public class ErrorRestartAppUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldTryRestart()
    {
        var shutdownHandler = Fixture.Freeze<Mock<IShutdownHandler>>();
        var action = Fixture.Create<ErrorRestartAppUseCase>();
        action.Execute();
        shutdownHandler.Verify(s => s.TryRestart(), Times.Once);
    }
}
