using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Ai;
using CaptureTool.Domain.Ai;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.Ai;

[TestClass]
public sealed class AiFeatureConsentServiceTests
{
    [TestMethod]
    public void GetConsentState_WhenSettingIsNotSet_ReturnsUnknown()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.IsSet(CaptureToolSettings.Settings_AiConsent_TextExtraction))
            .Returns(false);
        var service = new AiFeatureConsentService(settings.Object);

        service.GetConsentState(AiFeatureId.TextExtraction).Should().Be(AiFeatureConsentState.Unknown);
    }

    [TestMethod]
    public void GetConsentState_WhenSettingIsTrue_ReturnsGranted()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.IsSet(CaptureToolSettings.Settings_AiConsent_ImageSuperResolution))
            .Returns(true);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_AiConsent_ImageSuperResolution))
            .Returns(true);
        var service = new AiFeatureConsentService(settings.Object);

        service.GetConsentState(AiFeatureId.ImageSuperResolution).Should().Be(AiFeatureConsentState.Granted);
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsFeatureConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(AiFeatureId.TextExtraction, true, TestContext.CancellationToken);

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_AiConsent_TextExtraction, true), Times.Once);
        settings.Verify(service => service.TrySaveAsync(TestContext.CancellationToken), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
