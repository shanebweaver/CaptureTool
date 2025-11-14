using CaptureTool.Services.Cancellation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Services.Tests.Cancellation;

[TestClass]
public sealed class CancellationSeviceTests
{
    [TestMethod]
    public void GetLinkedCancellationTokenSource_ReturnsLinkedToken()
    {
        var service = new CancellationService();

        using var linked = service.GetLinkedCancellationTokenSource();

        Assert.IsNotNull(linked);
        Assert.IsFalse(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void GetLinkedCancellationTokenSource_WithExternalToken_LinksCorrectly()
    {
        var service = new CancellationService();
        using var externalCts = new CancellationTokenSource();

        using var linked = service.GetLinkedCancellationTokenSource(externalCts.Token);

        // Cancel the external token; linked should cancel
        externalCts.Cancel();

        Assert.IsTrue(linked.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void CancelAll_CancelsRootAndLinkedTokens()
    {
        var service = new CancellationService();

        using var linked = service.GetLinkedCancellationTokenSource();

        service.CancelAll();

        Assert.IsTrue(linked.Token.IsCancellationRequested);
    }

    [TestMethod]
    public async Task CancelAllAsync_CancelsRootAndLinkedTokens()
    {
        var service = new CancellationService();

        using var linked = service.GetLinkedCancellationTokenSource();

        await service.CancelAllAsync();

        Assert.IsTrue(linked.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Reset_ReplacesRootTokenSource_WhenAlreadyCancelled()
    {
        var service = new CancellationService();

        // Cancel the service to trigger reset logic
        service.CancelAll();

        // Act: Get a linked CTS, which should cause Reset() internally
        using var linked = service.GetLinkedCancellationTokenSource();

        Assert.IsFalse(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void GetLinkedCancellationTokenSource_AfterReset_UsesNewRoot()
    {
        var service = new CancellationService();

        // Cancel everything to force a reset condition
        service.CancelAll();

        // Force internal Reset
        using var first = service.GetLinkedCancellationTokenSource();

        Assert.IsFalse(first.IsCancellationRequested);

        // Cancel again to see if root works
        service.CancelAll();

        Assert.IsTrue(first.IsCancellationRequested);
    }

    [TestMethod]
    public void Reset_WhenNotCancelled_DoesNotCancelExistingTokens()
    {
        var service = new CancellationService();

        using var linked = service.GetLinkedCancellationTokenSource();

        service.Reset(); // Should reinitialize only if TryReset fails

        Assert.IsFalse(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_DisposesRootTokenSource()
    {
        var service = new CancellationService();

        service.Dispose();

        // Getting a linked token should now throw
        Assert.ThrowsException<ObjectDisposedException>(() =>
        {
            service.GetLinkedCancellationTokenSource();
        });
    }

    [TestMethod]
    public void LinkedTokenSource_CancelsWhenBothTokensCancel()
    {
        var service = new CancellationService();
        using var externalCts = new CancellationTokenSource();

        using var linked = service.GetLinkedCancellationTokenSource(externalCts.Token);

        // Cancel root
        service.CancelAll();

        Assert.IsTrue(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void GetLinkedCancellationTokenSource_ReturnsNewInstanceEveryCall()
    {
        var service = new CancellationService();

        using var linked1 = service.GetLinkedCancellationTokenSource();
        using var linked2 = service.GetLinkedCancellationTokenSource();

        Assert.AreNotSame(linked1, linked2);
    }
}
