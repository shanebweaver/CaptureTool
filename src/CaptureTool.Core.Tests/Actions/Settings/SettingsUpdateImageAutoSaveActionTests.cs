using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;
using Moq;

namespace CaptureTool.Core.Tests.Actions.Settings;

[TestClass]
public class SettingsUpdateImageAutoSaveActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldSetSetting_AndSave()
    {
        var settings = Fixture.Freeze<Mock<ISettingsService>>();
        var action = Fixture.Create<SettingsUpdateImageAutoSaveAction>();
        await action.ExecuteAsync(true);
        settings.Verify(s => s.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, true), Times.Once);
        settings.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
