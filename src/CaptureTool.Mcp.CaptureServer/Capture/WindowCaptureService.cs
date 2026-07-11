using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class WindowCaptureService : IWindowCaptureService
{
    private readonly IDisplayCaptureService _displayCaptureService;
    private readonly IScreenCapture _screenCapture;
    private readonly ICaptureKitImageCaptureAdapter _imageCaptureAdapter;

    public WindowCaptureService(
        IDisplayCaptureService displayCaptureService,
        IScreenCapture screenCapture,
        ICaptureKitImageCaptureAdapter imageCaptureAdapter)
    {
        _displayCaptureService = displayCaptureService;
        _screenCapture = screenCapture;
        _imageCaptureAdapter = imageCaptureAdapter;
    }

    public IReadOnlyList<WindowInfoDto> ListWindows()
    {
        MonitorCaptureResult[] monitors = GetMonitors();

        return [.. GetCandidateWindows(monitors).Select(window => new WindowInfoDto(
            GetWindowId(window.Handle),
            window.Title,
            RectangleDto.FromRectangle(window.Bounds)))];
    }

    public McpCapture CaptureWindow(string windowId)
    {
        nint handle = ParseWindowId(windowId);
        MonitorCaptureResult[] monitors = GetMonitors();
        CaptureWindow window = GetCandidateWindows(monitors)
            .FirstOrDefault(window => window.Handle == handle);

        if (window.Handle == 0)
        {
            throw new InvalidOperationException($"Window '{windowId}' is not available to capture.");
        }

        MonitorCaptureResult referenceMonitor = CaptureTargetSelection.GetBestMonitorForBounds(monitors, window.Bounds);

        return _imageCaptureAdapter.Capture(
            CaptureTarget.Window(window.Handle),
            window.Bounds,
            "window",
            referenceMonitor.Dpi,
            referenceMonitor.Scale,
            targetId: GetWindowId(window.Handle),
            targetTitle: window.Title,
            monitorBounds: referenceMonitor.MonitorBounds,
            workAreaBounds: referenceMonitor.WorkAreaBounds,
            isPrimary: referenceMonitor.IsPrimary);
    }

    private IReadOnlyList<CaptureWindow> GetCandidateWindows(IReadOnlyList<MonitorCaptureResult> monitors)
        => [.. _displayCaptureService.GetWindows()
            .Where(window =>
                window.Handle != 0
                && !string.IsNullOrWhiteSpace(window.Title)
                && window.Bounds.Width > 0
                && window.Bounds.Height > 0
                && IsOnCapturedDesktop(window, monitors))
            .OrderBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)];

    private static bool IsOnCapturedDesktop(CaptureWindow window, IReadOnlyList<MonitorCaptureResult> monitors)
        => monitors.Count == 0
            || monitors.Any(monitor => monitor.MonitorBounds.IntersectsWith(window.Bounds));

    private MonitorCaptureResult[] GetMonitors()
        => _screenCapture.CaptureAllMonitors() ?? [];

    private static string GetWindowId(nint handle)
        => $"hwnd:{handle}";

    private static nint ParseWindowId(string windowId)
    {
        const string Prefix = "hwnd:";
        if (string.IsNullOrWhiteSpace(windowId) || !windowId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Window ID must use the 'hwnd:' format.", nameof(windowId));
        }

        string value = windowId[Prefix.Length..];
        if (!long.TryParse(value, out long handle))
        {
            throw new ArgumentException("Window ID contains an invalid handle value.", nameof(windowId));
        }

        return new IntPtr(handle);
    }
}
