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

    [TestMethod]
    public void GetFeatureConsents_IncludesImageDescription()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        service.GetFeatureConsents().Should().Contain(consent =>
            consent.FeatureId == AiFeatureId.ImageDescription &&
            consent.DisplayName == "Image description");
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsImageDescriptionConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(AiFeatureId.ImageDescription, true, TestContext.CancellationToken);

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_AiConsent_ImageDescription, true), Times.Once);
    }

    [TestMethod]
    public void GetFeatureConsents_IncludesBackgroundRemoval()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        service.GetFeatureConsents().Should().Contain(consent =>
            consent.FeatureId == AiFeatureId.ImageForegroundExtraction &&
            consent.DisplayName == "Background removal");
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsBackgroundRemovalConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(AiFeatureId.ImageForegroundExtraction, true, TestContext.CancellationToken);

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_AiConsent_ImageForegroundExtraction, true), Times.Once);
    }

    [TestMethod]
    public void GetFeatureConsents_IncludesObjectErase()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        service.GetFeatureConsents().Should().Contain(consent =>
            consent.FeatureId == AiFeatureId.ImageObjectErase &&
            consent.DisplayName == "Object erase");
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsObjectEraseConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(AiFeatureId.ImageObjectErase, true, TestContext.CancellationToken);

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_AiConsent_ImageObjectErase, true), Times.Once);
    }

    [TestMethod]
    public void GetFeatureConsents_IncludesObjectExtraction()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        service.GetFeatureConsents().Should().Contain(consent =>
            consent.FeatureId == AiFeatureId.ImageObjectExtraction &&
            consent.DisplayName == "Object extraction");
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsObjectExtractionConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(AiFeatureId.ImageObjectExtraction, true, TestContext.CancellationToken);

        settings.Verify(service => service.Set(CaptureToolSettings.Settings_AiConsent_ImageObjectExtraction, true), Times.Once);
    }

    [TestMethod]
    public void GetFeatureConsents_IncludesVideoSuperResolution()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        service.GetFeatureConsents().Should().Contain(consent =>
            consent.FeatureId == AiFeatureId.VideoSuperResolution &&
            consent.DisplayName == "Video super resolution");
    }

    [TestMethod]
    public async Task SetConsentAsync_PersistsVideoSuperResolutionConsent()
    {
        var settings = new Mock<ISettingsService>();
        var service = new AiFeatureConsentService(settings.Object);

        await service.SetConsentAsync(
            AiFeatureId.VideoSuperResolution,
            true,
            TestContext.CancellationToken);

        settings.Verify(
            service => service.Set(
                CaptureToolSettings.Settings_AiConsent_VideoSuperResolution,
                true),
            Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
