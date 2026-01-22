using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.Actions.Loading;
using CaptureTool.Application.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Application.Tests.Actions.Loading;

[TestClass]
public class LoadingGoBackActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldGoBackOrHome()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var action = Fixture.Create<LoadingGoBackAction>();
        action.Execute();
        appNav.Verify(a => a.GoBackOrGoHome(), Times.Once);
    }
}
