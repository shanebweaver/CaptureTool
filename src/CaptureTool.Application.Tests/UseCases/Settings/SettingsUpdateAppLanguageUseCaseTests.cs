using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.UseCases.Settings;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Settings;

[TestClass]
public class SettingsUpdateAppLanguageUseCaseTests
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

        var action = Fixture.Create<SettingsUpdateAppLanguageAppCommand>();
        await action.ExecuteAsync(0);

        localization.Verify(l => l.OverrideLanguage(It.IsAny<IAppLanguage?>()), Times.Once);
        settings.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
