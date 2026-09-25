using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Ai;
using Moq;

namespace CaptureTool.Application.Tests.Ai;
[TestClass]
public sealed class TextExtractionConsentServiceTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task OnlyPersistedStandaloneApprovalAuthorizesOcr(bool allowed, bool saved)
    {
        bool? setting = null;
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.IsSet(CaptureToolSettings.Settings_AiConsent_TextExtraction)).Returns(() => setting != null);
        settings.Setup(x => x.Get(CaptureToolSettings.Settings_AiConsent_TextExtraction)).Returns(() => setting == true);
        settings.Setup(x => x.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, allowed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { if (saved) setting = allowed; return saved ? SettingsMutationResult.Saved : SettingsMutationResult.PersistenceFailed; });
        var prompt = new Mock<ITextExtractionConsentPrompt>();
        prompt.Setup(x => x.ConfirmAsync(It.IsAny<CancellationToken>())).ReturnsAsync(allowed);
        using var service = new TextExtractionConsentService(settings.Object, prompt.Object);
        Assert.AreEqual(AiFeatureConsentState.Unknown, service.State);
        Assert.AreEqual(allowed && saved, await service.EnsureConsentAsync());
        if (allowed && saved)
        {
            var token = service.Revoked;
            Assert.IsTrue(await service.EnsureConsentAsync());
            prompt.Verify(x => x.ConfirmAsync(It.IsAny<CancellationToken>()), Times.Once);
            setting = false;
            settings.Raise(x => x.SettingsChanged += null, new object[] { new ISettingDefinition[] { CaptureToolSettings.Settings_AiConsent_TextExtraction } });
            Assert.IsTrue(token.IsCancellationRequested);
        }
    }
    [TestMethod]
    public async Task CancelledPromptNeverPersistsApproval()
    {
        var settings = new Mock<ISettingsService>();
        var prompt = new Mock<ITextExtractionConsentPrompt>();
        using var cancellation = new CancellationTokenSource();
        prompt.Setup(x => x.ConfirmAsync(cancellation.Token)).ReturnsAsync(() => { cancellation.Cancel(); return true; });
        using var service = new TextExtractionConsentService(settings.Object, prompt.Object);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.EnsureConsentAsync(cancellation.Token));
        settings.Verify(x => x.TrySetAndSaveAsync(It.IsAny<IBoolSettingDefinition>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
