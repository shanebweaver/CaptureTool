using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class InMemoryMcpCaptureStoreTests
{
    [TestMethod]
    public void Store_WhenByteBudgetIsExceeded_EvictsOldestUntilNewCaptureFits()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptureBytes: 10, maxRetainedBytes: 10);
        store.Store(CreateCapture("first", 3));
        store.Store(CreateCapture("second", 3));
        store.Store(CreateCapture("third", 4));
        store.TryGet("first", out _).Should().BeTrue(); // Exact budget is allowed.

        store.Store(CreateCapture("new", 7));

        store.TryGet("first", out _).Should().BeFalse();
        store.TryGet("second", out _).Should().BeFalse();
        store.TryGet("third", out _).Should().BeFalse();
        store.TryGet("new", out _).Should().BeTrue();
    }

    [TestMethod]
    public void Store_WhenCountLimitIsExceeded_EvictsOldest()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptures: 2);
        store.Store(CreateCapture("first", 1));
        store.Store(CreateCapture("second", 1));

        store.Store(CreateCapture("third", 1));

        store.TryGet("first", out _).Should().BeFalse();
        store.TryGet("second", out _).Should().BeTrue();
        store.TryGet("third", out _).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(5L, 10L)]
    [DataRow(10L, 5L)]
    public void Store_WhenImageCannotFit_RejectsWithoutReplacingOrEvicting(long imageLimit, long totalLimit)
    {
        var store = new InMemoryMcpCaptureStore(maxCaptureBytes: imageLimit, maxRetainedBytes: totalLimit);
        McpCapture original = CreateCapture("original", 4);
        store.Store(original);

        Action replace = () => store.Store(CreateCapture("original", 6));
        Action add = () => store.Store(CreateCapture("too-large", 6));

        replace.Should().Throw<InvalidOperationException>().WithMessage("*Capture a smaller region and retry.*");
        add.Should().Throw<InvalidOperationException>().WithMessage("*storage limit*");
        store.TryGet("original", out McpCapture retained).Should().BeTrue();
        retained.Should().BeSameAs(original);
        store.TryGet("too-large", out _).Should().BeFalse();
    }

    [TestMethod]
    public void Store_ReplacingCapture_AccountsForNewBytesAndRefreshesEvictionOrder()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptures: 3, maxCaptureBytes: 10, maxRetainedBytes: 10);
        store.Store(CreateCapture("first", 4));
        store.Store(CreateCapture("second", 4));
        McpCapture replacement = CreateCapture("first", 6);

        store.Store(replacement);

        store.TryGet("second", out _).Should().BeTrue();
        store.TryGet("first", out McpCapture retained).Should().BeTrue();
        retained.Should().BeSameAs(replacement);
        store.Store(CreateCapture("third", 4));
        store.TryGet("second", out _).Should().BeFalse();
        store.TryGet("first", out _).Should().BeTrue();
        store.TryGet("third", out _).Should().BeTrue();

        store.Store(CreateCapture("first", 2));
        store.Store(CreateCapture("fourth", 4));
        store.TryGet("first", out _).Should().BeTrue();
        store.TryGet("third", out _).Should().BeTrue();
        store.TryGet("fourth", out _).Should().BeTrue();
    }

    [TestMethod]
    public void Store_WhenReplacementRequiresEviction_KeepsReplacement()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptureBytes: 10, maxRetainedBytes: 10);
        store.Store(CreateCapture("first", 4));
        store.Store(CreateCapture("second", 4));

        store.Store(CreateCapture("first", 10));

        store.TryGet("second", out _).Should().BeFalse();
        store.TryGet("first", out _).Should().BeTrue();
    }

    [TestMethod]
    public void Store_ConcurrentWrites_StayWithinByteBudget()
    {
        var store = new InMemoryMcpCaptureStore(maxCaptures: 100, maxCaptureBytes: 4, maxRetainedBytes: 12);

        Parallel.For(0, 100, index => store.Store(CreateCapture(index.ToString(), 4)));

        Enumerable.Range(0, 100).Count(index => store.TryGet(index.ToString(), out _)).Should().Be(3);
    }

    [TestMethod]
    [DataRow(0, 1L, 1L)]
    [DataRow(1, 0L, 1L)]
    [DataRow(1, 1L, 0L)]
    public void Constructor_RejectsNonPositiveLimits(int maxCaptures, long imageLimit, long totalLimit)
    {
        Action create = () => _ = new InMemoryMcpCaptureStore(maxCaptures, imageLimit, totalLimit);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static McpCapture CreateCapture(string id, int bytes)
        => new(new byte[bytes], McpCaptureMetadata.Create(
            id, DateTimeOffset.UnixEpoch, 1, 1, 96, 1, new Rectangle(0, 0, 1, 1), "region", "png"));
}
