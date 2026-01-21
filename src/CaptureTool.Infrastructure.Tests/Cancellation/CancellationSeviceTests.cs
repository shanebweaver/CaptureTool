using CaptureTool.Infrastructure.Implementations.Cancellation;

namespace CaptureTool.Infrastructure.Tests.Cancellation;

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

        service.CancelAll();

        using var linked = service.GetLinkedCancellationTokenSource();

        Assert.IsFalse(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void GetLinkedCancellationTokenSource_AfterReset_UsesNewRoot()
    {
        var service = new CancellationService();

        service.CancelAll();

        using var first = service.GetLinkedCancellationTokenSource();

        Assert.IsFalse(first.IsCancellationRequested);

        service.CancelAll();

        Assert.IsTrue(first.IsCancellationRequested);
    }

    [TestMethod]
    public void Reset_WhenNotCancelled_DoesNotCancelExistingTokens()
    {
        var service = new CancellationService();

        using var linked = service.GetLinkedCancellationTokenSource();

        service.Reset();

        Assert.IsFalse(linked.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_DisposesRootTokenSource()
    {
        var service = new CancellationService();

        service.Dispose();

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
