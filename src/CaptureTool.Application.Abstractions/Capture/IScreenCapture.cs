using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IScreenCapture
{
    MonitorCaptureResult[] CaptureAllMonitors();
    Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors);
    Bitmap CreateBitmapFromMonitorCaptureResult(MonitorCaptureResult monitor);
    Bitmap CreateCroppedBitmap(Bitmap image, Rectangle area, float scale);
    void SaveImageToFile(System.Drawing.Image image, string filePath);
}
