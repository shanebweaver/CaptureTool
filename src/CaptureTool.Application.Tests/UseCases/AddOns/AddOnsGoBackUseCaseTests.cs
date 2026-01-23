using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.AddOns;
using CaptureTool.Application.Interfaces.Navigation;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.AddOns;

[TestClass]
public class AddOnsGoBackUseCaseTests
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
        var action = Fixture.Create<AddOnsGoBackUseCase>();
        action.Execute();
        appNav.Verify(a => a.GoBackOrGoHome(), Times.Once);
    }
}
