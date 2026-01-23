using System.Drawing;

namespace CaptureTool.Domain.Capture.Interfaces;

public partial interface IScreenCapture
{
    MonitorCaptureResult[] CaptureAllMonitors();
    Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors);
    Bitmap CreateBitmapFromMonitorCaptureResult(MonitorCaptureResult monitor);
    Bitmap CreateCroppedBitmap(Bitmap image, Rectangle area, float scale);
    void SaveImageToFile(Image image, string filePath);
}