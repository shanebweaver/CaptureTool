using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Themes;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Settings;

[TestClass]
public class SettingsUpdateAppThemeUseCaseTests
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
        var action = Fixture.Create<SettingsUpdateAppThemeUseCase>();
        action.Execute(1);
        themes.Verify(t => t.UpdateCurrentTheme(It.IsAny<AppTheme>()), Times.Once);
    }
}
