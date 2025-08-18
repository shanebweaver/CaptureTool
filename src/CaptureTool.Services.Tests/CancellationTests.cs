using CaptureTool.Services.Cancellation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace CaptureTool.Services.Tests;

[TestClass]
public sealed class CancellationTests
{
    private CancellationService _cancellationService = null!;

    [TestInitialize]
    public void Setup()
    {
        _cancellationService = new();
    }

    [TestMethod]
    public async Task CancellationTest()
    {
        var linkedSource = _cancellationService.GetLinkedCancellationTokenSource();
        Assert.IsFalse(linkedSource.IsCancellationRequested);

        // Cancel all
        _cancellationService.CancelAll();
        Assert.IsTrue(linkedSource.IsCancellationRequested);

        // Force reset
        _cancellationService.Reset();

        // Create a new token source
        var linkedSource2 = _cancellationService.GetLinkedCancellationTokenSource();
        Assert.IsTrue(linkedSource.IsCancellationRequested);
        Assert.IsFalse(linkedSource2.IsCancellationRequested);

        // Cancel all async
        await _cancellationService.CancelAllAsync();
        Assert.IsTrue(linkedSource2.IsCancellationRequested);

        // Create a new token source, but don't reset this time.
        var linkedSource3 = _cancellationService.GetLinkedCancellationTokenSource();
        Assert.IsTrue(linkedSource.IsCancellationRequested);
        Assert.IsTrue(linkedSource2.IsCancellationRequested);
        Assert.IsFalse(linkedSource3.IsCancellationRequested);
    }
}
