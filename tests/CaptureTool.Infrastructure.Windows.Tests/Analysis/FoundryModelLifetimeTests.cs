using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.AI.Foundry.Local;
using Moq;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class FoundryModelLifetimeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OnlyRequestedTextModelsAreEligibleForThirtySecondsOfIdleReuse()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var text = Model("text");
        var vision = Model("vision");
        Assert.AreEqual(TimeSpan.Zero, runtime.IdleRetention);
        await using (await runtime.AcquireModelAsync(text.Object, TestContext.CancellationToken, retainWhenIdle: true)) { }
        Assert.AreEqual(TimeSpan.FromSeconds(30), runtime.IdleRetention);
        await using (await runtime.AcquireModelAsync(text.Object, TestContext.CancellationToken, retainWhenIdle: true)) { }
        text.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Once);
        await runtime.ReleaseAsync();
        Assert.AreEqual(TimeSpan.Zero, runtime.IdleRetention);
        await using (await runtime.AcquireModelAsync(text.Object, TestContext.CancellationToken, retainWhenIdle: true)) { }
        text.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        await using (await runtime.AcquireModelAsync(vision.Object, TestContext.CancellationToken)) { }
        Assert.AreEqual(TimeSpan.Zero, runtime.IdleRetention);
        await runtime.ReleaseAsync();
    }

    [TestMethod]
    public async Task SameModelIsLoadedOnceAcrossAdaptersAndReleasedWhenIdle()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var first = Model("text:1");
        var second = Model("text:1");
        for (int i = 0; i < 6; i++)
            await using (await runtime.AcquireModelAsync(i % 2 == 0 ? first.Object : second.Object, TestContext.CancellationToken)) { }
        first.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Never);
        first.Verify(model => model.UnloadAsync(It.IsAny<CancellationToken>()), Times.Never);
        await runtime.ReleaseAsync();
        first.Verify(model => model.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        await runtime.ReleaseAsync();
        first.Verify(model => model.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SwitchingModelsUnloadsBeforeLoadingAndNeverOverlapsInference()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var calls = new List<string>();
        var vision = Model("vision"); var text = Model("text");
        vision.Setup(model => model.UnloadAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("unload")).Returns(Task.CompletedTask);
        text.Setup(model => model.LoadAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("load")).Returns(Task.CompletedTask);
        var active = await runtime.AcquireModelAsync(vision.Object, TestContext.CancellationToken);
        Task<IAsyncDisposable> next = runtime.AcquireModelAsync(text.Object, TestContext.CancellationToken);
        Assert.IsFalse(next.IsCompleted);
        Assert.IsEmpty(calls);
        await active.DisposeAsync();
        await using (await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken)) { }
        CollectionAssert.AreEqual(new[] { "unload", "load" }, calls);
        await runtime.ReleaseAsync();
    }

    [TestMethod]
    public async Task CancellationDoesNotUnloadUntilTheNativeInvocationReleasesItsLease()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var model = Model("text");
        using var cancellation = new CancellationTokenSource();
        var active = await runtime.AcquireModelAsync(model.Object, cancellation.Token);
        cancellation.Cancel();
        Task release = runtime.ReleaseAsync();
        Assert.IsFalse(release.IsCompleted);
        model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Never);
        await active.DisposeAsync();
        await release.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(AnalysisMediaKind.Image, false)]
    [DataRow(AnalysisMediaKind.Image, true)]
    [DataRow(AnalysisMediaKind.Video, false)]
    [DataRow(AnalysisMediaKind.Video, true)]
    public async Task WindowsImageAnalysisWaitsForFoundryAndReleasesItBeforeDecoding(AnalysisMediaKind kind, bool description)
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var model = Model("retained-text");
        var active = await runtime.AcquireModelAsync(model.Object, TestContext.CancellationToken, retainWhenIdle: true);
        try
        {
            var analyzer = new WindowsImageAnalyzer("test", description ? WindowsImageModel.Description : WindowsImageModel.Ocr,
                kind, new WindowsAnalysisMedia(Mock.Of<IScratchArtifactStore>()), runtime);
            // Passive UI probes must not evict an active or retained model.
            _ = await analyzer.GetAvailabilityAsync(kind, null, TestContext.CancellationToken);
            model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Never);
            var input = new AnalysisInput(CaptureId.New(), kind, new(new string('a', 64)),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.png"));
            Task<AnalyzerOutcome> pending = analyzer.AnalyzeAsync(input, null, TestContext.CancellationToken);
            Assert.IsFalse(pending.IsCompleted, "Decoding must wait while Foundry still owns an inference lease.");
            model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Never);
            await active.DisposeAsync();
            active = null;
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            Assert.AreEqual(AnalyzerOutcomeKind.InvalidSource, result.Kind);
            model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(TimeSpan.Zero, runtime.IdleRetention);
        }
        finally { if (active != null) await active.DisposeAsync(); }
    }

    [TestMethod]
    public async Task CancellingWindowsHandoffWaitsForFoundryThenStopsBeforeDecoding()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var model = Model("speech");
        var active = await runtime.AcquireModelAsync(model.Object, TestContext.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var analyzer = new WindowsImageAnalyzer("test", WindowsImageModel.Description, AnalysisMediaKind.Video,
                new WindowsAnalysisMedia(Mock.Of<IScratchArtifactStore>()), runtime);
            var input = new AnalysisInput(CaptureId.New(), AnalysisMediaKind.Video, new(new string('a', 64)), "invalid-path");
            Task<AnalyzerOutcome> pending = analyzer.AnalyzeAsync(input, null, cancellation.Token);
            await cancellation.CancelAsync();
            Assert.IsFalse(pending.IsCompleted);
            model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Never);
            await active.DisposeAsync();
            active = null;
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
            model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { if (active != null) await active.DisposeAsync(); }
    }

    [TestMethod]
    public async Task FailedUnloadBlocksReplacementAndDoesNotTreatOldModelAsReady()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var first = Model("first"); var second = Model("second");
        first.SetupSequence(model => model.UnloadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("unload failed")).Returns(Task.CompletedTask).Returns(Task.CompletedTask);
        await using (await runtime.AcquireModelAsync(first.Object, TestContext.CancellationToken)) { }
        await Assert.ThrowsExactlyAsync<IOException>(() => runtime.AcquireModelAsync(second.Object, TestContext.CancellationToken));
        second.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Never);
        await using (await runtime.AcquireModelAsync(first.Object, TestContext.CancellationToken)) { }
        first.Verify(model => model.LoadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        await runtime.ReleaseAsync();
    }

    [TestMethod]
    public async Task FailedLoadIsCleanedAndCanBeRetriedWithoutLeakingOwnership()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>());
        var model = Model("text");
        model.SetupSequence(value => value.LoadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("load failed")).Returns(Task.CompletedTask);
        await Assert.ThrowsExactlyAsync<IOException>(() => runtime.AcquireModelAsync(model.Object, TestContext.CancellationToken));
        model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        await using (await runtime.AcquireModelAsync(model.Object, TestContext.CancellationToken)) { }
        await runtime.ReleaseAsync();
        model.Verify(value => value.UnloadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Mock<IModel> Model(string id)
    {
        var model = new Mock<IModel>(MockBehavior.Strict);
        model.SetupGet(value => value.Id).Returns(id);
        model.Setup(value => value.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        model.Setup(value => value.UnloadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return model;
    }
}
