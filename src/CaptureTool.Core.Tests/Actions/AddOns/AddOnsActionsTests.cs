using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.AddOns;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using FluentAssertions;
using Moq;

namespace CaptureTool.Core.Tests.Actions.AddOns;

[TestClass]
public class AddOnsActionsTests
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
        var goBack = Fixture.Freeze<Mock<IAddOnsGoBackAction>>();
        goBack.Setup(a => a.CanExecute()).Returns(true);

        var actions = new AddOnsActions(goBack.Object);
        actions.GoBack();

        goBack.Verify(a => a.CanExecute(), Times.Once);
        goBack.Verify(a => a.Execute(), Times.Once);
    }

    [TestMethod]
    public void CanGoBack_ShouldReturnFromUnderlying()
    {
        var goBack = Fixture.Freeze<Mock<IAddOnsGoBackAction>>();
        goBack.Setup(a => a.CanExecute()).Returns(false);

        var actions = new AddOnsActions(goBack.Object);
        bool result = actions.CanGoBack();

        result.Should().BeFalse();
        goBack.Verify(a => a.CanExecute(), Times.Once);
    }
}
