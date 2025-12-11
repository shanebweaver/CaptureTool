using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Loading;
using CaptureTool.Core.Interfaces.Actions.Loading;
using Moq;

namespace CaptureTool.Core.Tests.Actions;

[TestClass]
public class LoadingActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void GoBack_ShouldDelegateToAction()
    {
        var goBack = Fixture.Freeze<Mock<ILoadingGoBackAction>>();
        goBack.Setup(a => a.CanExecute()).Returns(true);

        var actions = new LoadingActions(goBack.Object);
        actions.GoBack();

        goBack.Verify(a => a.CanExecute(), Times.Once);
        goBack.Verify(a => a.Execute(), Times.Once);
    }
}
