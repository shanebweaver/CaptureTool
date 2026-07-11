using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureKit.Abstractions;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class PrimaryMonitorCaptureService : IPrimaryMonitorCaptureService
{
    private readonly IScreenCapture _screenCapture;
    private readonly ICaptureKitImageCaptureAdapter _imageCaptureAdapter;

    public PrimaryMonitorCaptureService(IScreenCapture screenCapture, ICaptureKitImageCaptureAdapter imageCaptureAdapter)
    {
        _screenCapture = screenCapture;
        _imageCaptureAdapter = imageCaptureAdapter;
    }

    public McpCapture Capture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        MonitorCaptureResult primaryMonitor = CaptureTargetSelection.GetPrimaryMonitor(monitors);

        return _imageCaptureAdapter.Capture(
            CaptureTarget.Monitor(primaryMonitor.HMonitor),
            primaryMonitor.MonitorBounds,
            "primaryMonitor",
            primaryMonitor.Dpi,
            primaryMonitor.Scale,
            targetId: $"hmonitor:{primaryMonitor.HMonitor}",
            targetTitle: "Primary monitor",
            monitorBounds: primaryMonitor.MonitorBounds,
            workAreaBounds: primaryMonitor.WorkAreaBounds,
            isPrimary: primaryMonitor.IsPrimary);
    }
}
