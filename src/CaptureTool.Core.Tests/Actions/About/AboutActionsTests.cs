using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.About;
using CaptureTool.Core.Interfaces.Actions.About;
using FluentAssertions;
using Moq;

namespace CaptureTool.Core.Tests.Actions.About;

[TestClass]
public class AboutActionsTests
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
        var goBack = Fixture.Freeze<Mock<IAboutGoBackAction>>();
        goBack.Setup(a => a.CanExecute()).Returns(true);

        var actions = new AboutActions(goBack.Object);
        actions.GoBack();

        goBack.Verify(a => a.CanExecute(), Times.Once);
        goBack.Verify(a => a.Execute(), Times.Once);
    }

    [TestMethod]
    public void CanGoBack_ShouldReturnFromUnderlying()
    {
        var goBack = Fixture.Freeze<Mock<IAboutGoBackAction>>();
        goBack.Setup(a => a.CanExecute()).Returns(false);

        var actions = new AboutActions(goBack.Object);
        bool result = actions.CanGoBack();

        result.Should().BeFalse();
        goBack.Verify(a => a.CanExecute(), Times.Once);
    }
}
