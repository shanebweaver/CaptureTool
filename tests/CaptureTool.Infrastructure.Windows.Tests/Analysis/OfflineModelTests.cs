using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using Microsoft.AI.Foundry.Local;
using Moq;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class OfflineModelTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MissingModelOfflineDoesNotStartDownloadAndCanRetryAfterReconnect()
    {
        bool offline = true;
        var guard = new ModelDownloadGuard(() => offline);
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>(), downloads: guard);
        var model = new Mock<IModel>(MockBehavior.Strict);
        model.Setup(value => value.IsCachedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsExactlyAsync<ModelDownloadRequiredException>(() =>
            runtime.EnsureCachedAsync(model.Object, null, TestContext.CancellationToken));
        model.Verify(value => value.DownloadAsync(It.IsAny<Action<float>>(), It.IsAny<CancellationToken>()), Times.Never);

        offline = false;
        model.Setup(value => value.DownloadAsync(It.IsAny<Action<float>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await runtime.EnsureCachedAsync(model.Object, null, TestContext.CancellationToken);
        model.Verify(value => value.DownloadAsync(It.IsAny<Action<float>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CachedModelRemainsAvailableOfflineWithoutDownloading()
    {
        using var runtime = new FoundryRuntime(Mock.Of<IStorageService>(), downloads: new(() => true));
        var model = new Mock<IModel>(MockBehavior.Strict);
        model.Setup(value => value.IsCachedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await runtime.EnsureCachedAsync(model.Object, null, TestContext.CancellationToken);
        model.Verify(value => value.DownloadAsync(It.IsAny<Action<float>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task FirstUseOfflineDoesNotInitializeFoundryOrCreateACache()
    {
        string root = Path.Combine(Path.GetTempPath(), "CaptureTool-offline-" + Guid.NewGuid());
        var storage = new Mock<IStorageService>();
        storage.Setup(value => value.GetApplicationDataFolderPath()).Returns(root);
        var device = Mock.Of<IAiDeviceSupport>(value => value.FoundryCpu);
        using var runtime = new FoundryRuntime(storage.Object, device, new(() => true));
        await Assert.ThrowsExactlyAsync<ModelDownloadRequiredException>(() =>
            runtime.ResolveAsync("whisper-tiny", TestContext.CancellationToken));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public async Task DisconnectCancelsDownloadAndWaitsForItsCleanup()
    {
        bool offline = false;
        bool stopped = false;
        var guard = new ModelDownloadGuard(() => offline);
        Task preparation = guard.RunAsync(async token =>
        {
            offline = true;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { stopped = true; }
        }, TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<ModelDownloadRequiredException>(() => preparation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(stopped);
    }

    [TestMethod]
    public async Task UserCancellationIsNotReportedAsMissingInternet()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new ModelDownloadGuard(() => true).RunAsync(_ => Task.CompletedTask, cancellation.Token));
    }
}
