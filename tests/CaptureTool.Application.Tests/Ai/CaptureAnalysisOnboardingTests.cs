using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using Moq;
namespace CaptureTool.Application.Tests.Ai;
[TestClass]
public sealed class CaptureAnalysisOnboardingTests
{
    [TestMethod]
    public async Task OnlyExplicitEntryPromptsAndDeclinedConsentCanBeRequestedAgain()
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State());
        using var service = new CaptureAnalysisOnboarding(memory.Object);
        memory.VerifyNoOtherCalls();
        Assert.IsFalse(await service.EnableAsync());
        Assert.IsFalse(await service.EnableAsync());
        memory.Verify(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
    [TestMethod]
    public async Task OverlappingExplicitActionsShareOnePrompt()
    {
        var pending = new TaskCompletionSource();
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State());
        memory.Setup(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>())).Returns(pending.Task);
        using var service = new CaptureAnalysisOnboarding(memory.Object);
        var first = service.EnableAsync();
        var feature = service.EnableAsync();
        pending.SetResult();
        await Task.WhenAll(first, feature);
        memory.Verify(x => x.SetConsentAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public async Task ExistingConsentOrUnavailablePolicyDoesNotPrompt(bool consent, bool available)
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State() with { PolicyAvailable = available, Policy = new(false, consent, Guid.NewGuid(), 0) });
        using var service = new CaptureAnalysisOnboarding(memory.Object);
        await service.EnableAsync();
        memory.Verify(x => x.SetConsentAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    private static CaptureMemoryState State() => new(CaptureMemoryPolicy.Disabled(), true, new(false, true), new(AnalysisActivity.Idle));
}
