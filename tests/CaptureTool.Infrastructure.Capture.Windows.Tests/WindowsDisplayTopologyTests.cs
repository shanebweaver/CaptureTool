using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsDisplayTopologyTests
{
    [TestMethod]
    public void GetMonitorBounds_EnumeratesNativeDesktopGeometry()
    {
        var topology = new WindowsDisplayTopology();

        IReadOnlyList<Rectangle> bounds = topology.GetMonitorBounds();

        // Headless sessions may have no monitors; any enumerated monitor must have usable geometry.
        bounds.Should().OnlyContain(rectangle => rectangle.Width > 0 && rectangle.Height > 0);
    }
}
