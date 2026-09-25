using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Analysis;
using Moq;
namespace CaptureTool.Application.Tests.Ai;
[TestClass]
public sealed class CaptureAnalysisOnboardingTests
{
    [TestMethod]
    public async Task DeclinedWelcomeIsRememberedButExplicitEntryCanAskAgain()
    {
        bool seen = false;
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.Get(CaptureToolSettings.Settings_CaptureAnalysis_OnboardingSeen)).Returns(() => seen);
        settings.Setup(x => x.TrySetAndSaveAsync(CaptureToolSettings.Settings_CaptureAnalysis_OnboardingSeen, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { seen = true; return SettingsMutationResult.Saved; });
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State());
        using (var first = new CaptureAnalysisOnboarding(memory.Object, settings.Object))
        {
            await first.ShowOnFirstLaunchAsync();
            await first.ShowOnFirstLaunchAsync();
        }
        using var restart = new CaptureAnalysisOnboarding(memory.Object, settings.Object);
        await restart.ShowOnFirstLaunchAsync();
        memory.Verify(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>()), Times.Once);
        await restart.EnableAsync();
        memory.Verify(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
    [TestMethod]
    public async Task OverlappingStartupAndFeatureRequestsShareOnePrompt()
    {
        var pending = new TaskCompletionSource();
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State());
        memory.Setup(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var service = new CaptureAnalysisOnboarding(memory.Object, Mock.Of<ISettingsService>());
        var startup = service.ShowOnFirstLaunchAsync();
        var feature = service.EnableAsync();
        pending.SetResult();
        await Task.WhenAll(startup, feature);
        memory.Verify(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public async Task ExistingConsentOrUnavailablePolicyDoesNotPromptAtStartup(bool consent, bool available)
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State() with { PolicyAvailable = available, Policy = new(false, consent, Guid.NewGuid(), 0) });
        using var service = new CaptureAnalysisOnboarding(memory.Object, Mock.Of<ISettingsService>());
        await service.ShowOnFirstLaunchAsync();
        memory.Verify(x => x.SetScanningAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        memory.Verify(x => x.SetConsentAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    private static CaptureMemoryState State() => new(CaptureMemoryPolicy.Disabled(), true, new(false, true), new(AnalysisActivity.Idle));
}
