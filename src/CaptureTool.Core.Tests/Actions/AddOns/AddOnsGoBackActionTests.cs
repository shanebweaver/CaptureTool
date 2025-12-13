using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.AddOns;
using CaptureTool.Core.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Core.Tests.Actions.AddOns;

[TestClass]
public class AddOnsGoBackActionTests
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
        var action = Fixture.Create<AddOnsGoBackAction>();
        action.Execute();
        appNav.Verify(a => a.GoBackOrGoHome(), Times.Once);
    }
}
