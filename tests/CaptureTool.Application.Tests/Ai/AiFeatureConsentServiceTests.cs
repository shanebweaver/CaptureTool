using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Ai;
using CaptureTool.Domain.Ai;
using Moq;

namespace CaptureTool.Application.Tests.Ai;

[TestClass]
public sealed class AiFeatureConsentServiceTests
{
    [TestMethod]
    public void EveryFeatureSharesConsentAndDisablingScanningDoesNotRevokeEditing()
    {
        CaptureMemoryState state = State(true, true);
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(() => state);
        using var service = new AiFeatureConsentService(memory.Object);
        CancellationToken authorized = service.Revoked;
        foreach (AiFeatureId id in Enum.GetValues<AiFeatureId>())
            Assert.AreEqual(AiFeatureConsentState.Granted, service.GetConsentState(id));

        state = State(true, false);
        memory.Raise(x => x.StateChanged += null);
        Assert.AreEqual(authorized, service.Revoked);
        Assert.IsFalse(authorized.IsCancellationRequested);
    }

    [TestMethod]
    public void RevocationAndUnavailablePolicyCancelOldWorkAndRegrantCannotReviveIt()
    {
        CaptureMemoryState state = State(true);
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(() => state);
        using var service = new AiFeatureConsentService(memory.Object);
        CancellationToken original = service.Revoked;
        state = state with { ConsentAvailable = false };
        memory.Raise(x => x.StateChanged += null);
        Assert.IsTrue(original.IsCancellationRequested);
        Assert.AreEqual(AiFeatureConsentState.Unknown, service.GetConsentState(AiFeatureId.TextExtraction));

        state = State(false);
        memory.Raise(x => x.StateChanged += null);
        Assert.AreEqual(AiFeatureConsentState.Denied, service.GetConsentState(AiFeatureId.ImageDescription));
        state = State(true);
        memory.Raise(x => x.StateChanged += null);
        Assert.IsTrue(original.IsCancellationRequested);
        Assert.IsFalse(service.Revoked.IsCancellationRequested);
        Assert.AreNotEqual(original, service.Revoked);
        CancellationToken next = service.Revoked;
        state = State(false);
        memory.Raise(x => x.StateChanged += null);
        Assert.IsTrue(next.IsCancellationRequested);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OnUseConsentDelegatesToTheSingleProtectedPolicyOwner(bool approved)
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(State(false));
        using var cancellation = new CancellationTokenSource();
        memory.Setup(x => x.EnsureConsentAsync(cancellation.Token)).ReturnsAsync(approved);
        using var service = new AiFeatureConsentService(memory.Object);
        Assert.AreEqual(approved, await service.EnsureConsentAsync(cancellation.Token));
        memory.Verify(x => x.EnsureConsentAsync(cancellation.Token), Times.Once);
    }

    private static CaptureMemoryState State(bool consent, bool scanning = false) =>
        new(new(scanning, consent, Guid.NewGuid(), 0), true, new(false, true), new(AnalysisActivity.Idle));
}
