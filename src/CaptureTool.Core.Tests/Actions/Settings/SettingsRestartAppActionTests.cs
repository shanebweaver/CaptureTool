using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Settings;

[TestClass]
public class SettingsRestartAppActionTests
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
        var action = Fixture.Create<SettingsRestartAppAction>();
        action.Execute();
        shutdown.Verify(s => s.TryRestart(), Times.Once);
    }
}
