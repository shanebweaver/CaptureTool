using CaptureTool.Application.Abstractions.Capture;
using Windows.Graphics.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows;

internal sealed class WindowsVideoCaptureSupportService : IVideoCaptureSupportService
{
    private readonly Func<bool> _isOperatingSystemSupported;
    private readonly Func<bool> _isGraphicsCaptureSupported;

    public WindowsVideoCaptureSupportService()
        : this(
            () => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362),
            () => GraphicsCaptureSession.IsSupported())
    {
    }

    internal WindowsVideoCaptureSupportService(
        Func<bool> isOperatingSystemSupported,
        Func<bool> isGraphicsCaptureSupported)
    {
        _isOperatingSystemSupported = isOperatingSystemSupported;
        _isGraphicsCaptureSupported = isGraphicsCaptureSupported;
    }

    public VideoCaptureSupportStatus GetSupportStatus()
    {
        if (!_isOperatingSystemSupported())
        {
            return VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.OperatingSystem);
        }

        try
        {
            return _isGraphicsCaptureSupported()
                ? VideoCaptureSupportStatus.Supported
                : VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture);
        }
        catch (Exception)
        {
            // Treat activation and platform-probe failures as unsupported. The feature must not
            // attempt capture when Windows cannot reliably answer the support query.
            return VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture);
        }
    }
}
