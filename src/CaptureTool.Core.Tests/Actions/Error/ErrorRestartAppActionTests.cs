using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Error;
using CaptureTool.Services.Interfaces.Shutdown;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Error;

[TestClass]
public class ErrorRestartAppActionTests
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
        var action = Fixture.Create<ErrorRestartAppAction>();
        action.Execute();
        shutdownHandler.Verify(s => s.TryRestart(), Times.Once);
    }
}
