using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Settings;

[TestClass]
public class SettingsUpdateImageAutoCopyUseCaseTests
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
        var action = Fixture.Create<SettingsUpdateImageAutoCopyAppCommand>();
        await action.ExecuteAsync(true);
        settings.Verify(s => s.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, true), Times.Once);
        settings.Verify(s => s.TrySaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
