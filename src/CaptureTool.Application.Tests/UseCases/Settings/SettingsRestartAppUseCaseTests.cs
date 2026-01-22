using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Settings;

[TestClass]
public class SettingsRestartAppUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldRestartApp()
    {
        var shutdown = Fixture.Freeze<Mock<IShutdownHandler>>();
        var action = Fixture.Create<SettingsRestartAppUseCase>();
        action.Execute();
        shutdown.Verify(s => s.TryRestart(), Times.Once);
    }
}
