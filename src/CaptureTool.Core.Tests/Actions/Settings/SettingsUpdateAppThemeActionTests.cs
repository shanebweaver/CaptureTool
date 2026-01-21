using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Themes;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Settings;

[TestClass]
public class SettingsUpdateAppThemeActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldUpdateTheme_WhenIndexValid()
    {
        var themes = Fixture.Freeze<Mock<IThemeService>>();
        var action = Fixture.Create<SettingsUpdateAppThemeAction>();
        action.Execute(1);
        themes.Verify(t => t.UpdateCurrentTheme(It.IsAny<AppTheme>()), Times.Once);
    }
}
