using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Settings;

[TestClass]
public class SettingsUpdateAppLanguageActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldUpdateOverride_AndSave()
    {
        var localization = Fixture.Freeze<Mock<ILocalizationService>>();
        var settings = Fixture.Freeze<Mock<ISettingsService>>();

        localization.SetupGet(l => l.SupportedLanguages).Returns(new[] { Fixture.Create<IAppLanguage>(), Fixture.Create<IAppLanguage>() });

        var action = Fixture.Create<SettingsUpdateAppLanguageAction>();
        await action.ExecuteAsync(0);

        localization.Verify(l => l.OverrideLanguage(It.IsAny<IAppLanguage?>()), Times.Once);
        settings.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
