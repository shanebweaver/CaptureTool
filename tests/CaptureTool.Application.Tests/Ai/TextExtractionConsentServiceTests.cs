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
    [DataRow(true)]
    [DataRow(false)]
    public async Task RevocationImmediatelyCancelsOcrEvenWhilePersistenceIsPendingOrFails(bool saved)
    {
        bool setting = true;
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.IsSet(CaptureToolSettings.Settings_AiConsent_TextExtraction)).Returns(true);
        settings.Setup(x => x.Get(CaptureToolSettings.Settings_AiConsent_TextExtraction)).Returns(() => setting);
        var pending = new TaskCompletionSource<SettingsMutationResult>();
        settings.Setup(x => x.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, false, It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var service = new TextExtractionConsentService(settings.Object, Mock.Of<ITextExtractionConsentPrompt>());
        var token = service.Revoked;
        int changes = 0;
        service.StateChanged += () => changes++;
        var revoke = service.RevokeConsentAsync();
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.AreEqual(AiFeatureConsentState.Denied, service.State);
        if (saved) setting = false;
        pending.SetResult(saved ? SettingsMutationResult.Saved : SettingsMutationResult.PersistenceFailed);
        Assert.AreEqual(saved, await revoke);
        Assert.AreEqual(AiFeatureConsentState.Denied, service.State);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public async Task ANewerSettingsRevocationRejectsAnEarlierConsentPrompt()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SettingsMutationResult.Saved);
        var pending = new TaskCompletionSource<bool>();
        var prompt = new Mock<ITextExtractionConsentPrompt>();
        prompt.Setup(x => x.ConfirmAsync(It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var service = new TextExtractionConsentService(settings.Object, prompt.Object);
        var grant = service.EnsureConsentAsync();
        var revoke = service.RevokeConsentAsync();
        pending.SetResult(true);
        Assert.IsFalse(await grant);
        Assert.IsTrue(await revoke);
        settings.Verify(x => x.TrySetAndSaveAsync(CaptureToolSettings.Settings_AiConsent_TextExtraction, true, It.IsAny<CancellationToken>()), Times.Never);
    }

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
