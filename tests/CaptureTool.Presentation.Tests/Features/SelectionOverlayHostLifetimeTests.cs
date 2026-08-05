using CaptureTool.Presentation.Features.SelectionOverlay;
using FluentAssertions;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SelectionOverlayHostLifetimeTests
{
    [TestMethod]
    public void Dispose_WithoutClose_InvokesCleanupOnce()
    {
        int cleanupCount = 0;
        var lifetime = new SelectionOverlayHostLifetime(() => cleanupCount++);

        lifetime.Dispose();

        cleanupCount.Should().Be(1);
        lifetime.IsClosed.Should().BeTrue();
        lifetime.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void Close_ThenDispose_InvokesCleanupOnce()
    {
        int cleanupCount = 0;
        var lifetime = new SelectionOverlayHostLifetime(() => cleanupCount++);

        lifetime.Close();
        lifetime.Dispose();

        cleanupCount.Should().Be(1);
        lifetime.IsClosed.Should().BeTrue();
        lifetime.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void Dispose_Repeatedly_InvokesCleanupOnce()
    {
        int cleanupCount = 0;
        var lifetime = new SelectionOverlayHostLifetime(() => cleanupCount++);

        lifetime.Dispose();
        lifetime.Dispose();
        lifetime.Close();

        cleanupCount.Should().Be(1);
    }

    [TestMethod]
    public void Close_Repeatedly_InvokesCleanupOnce()
    {
        int cleanupCount = 0;
        var lifetime = new SelectionOverlayHostLifetime(() => cleanupCount++);

        lifetime.Close();
        lifetime.Close();

        cleanupCount.Should().Be(1);
        lifetime.IsClosed.Should().BeTrue();
        lifetime.IsDisposed.Should().BeFalse();
    }

    [TestMethod]
    public void Dispose_WithNoInitializedResources_CompletesSafely()
    {
        var lifetime = new SelectionOverlayHostLifetime(() => { });
        Action action = lifetime.Dispose;

        action.Should().NotThrow();
        lifetime.IsClosed.Should().BeTrue();
        lifetime.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void Close_WhenCleanupReentersClose_InvokesCleanupOnce()
    {
        int cleanupCount = 0;
        SelectionOverlayHostLifetime? lifetime = null;
        lifetime = new SelectionOverlayHostLifetime(() =>
        {
            cleanupCount++;
            lifetime!.Close();
        });

        lifetime.Close();

        cleanupCount.Should().Be(1);
    }
}
