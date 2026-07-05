using CaptureKit.Abstractions;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows;

public static partial class WindowInfoHelper
{
    private static readonly IDisplayCaptureService DisplayCaptureService = new CaptureKit.Windows.DisplayCaptureService();

    public static List<WindowInfo> GetAllWindows()
        => [.. DisplayCaptureService.GetWindows()
            .Where(window => window.Title is not "Windows Input Experience" and not "Settings")
            .Select(window => new WindowInfo(window.Handle, window.Title, window.Bounds))];
}
