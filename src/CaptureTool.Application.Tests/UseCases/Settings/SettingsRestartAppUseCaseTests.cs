using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Settings;
using CaptureTool.Infrastructure.Abstractions.Shutdown;
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
        var action = Fixture.Create<SettingsRestartApplicationAppCommand>();
        action.Execute();
        shutdown.Verify(s => s.TryRestart(), Times.Once);
    }
}
