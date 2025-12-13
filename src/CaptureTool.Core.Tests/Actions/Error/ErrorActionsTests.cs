using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Error;
using CaptureTool.Core.Interfaces.Actions.Error;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Error;

[TestClass]
public class ErrorActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void RestartApp_ShouldDelegateToAction()
    {
        var restartApp = Fixture.Freeze<Mock<IErrorRestartAppAction>>();
        restartApp.Setup(a => a.CanExecute()).Returns(true);

        var actions = new ErrorActions(restartApp.Object);
        actions.RestartApp();

        restartApp.Verify(a => a.CanExecute(), Times.Once);
        restartApp.Verify(a => a.Execute(), Times.Once);
    }
}
