using CaptureTool.Domains.Capture.Interfaces;
using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

internal static partial class CaptureInterop
{
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool TryStartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false);

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryPauseRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryResumeRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryStopRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryToggleAudioCapture(bool enabled);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetVideoFrameCallback(VideoFrameCallback? callback);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioSampleCallback(AudioSampleCallback? callback);

    // Screenshot capture functions
    [DllImport("CaptureInterop.dll")]
    internal static extern IntPtr CaptureMonitorScreenshot(IntPtr hMonitor);

    [DllImport("CaptureInterop.dll")]
    internal static extern IntPtr CaptureAllMonitorsScreenshot();

    [DllImport("CaptureInterop.dll")]
    internal static extern void GetScreenshotInfo(
        IntPtr handle,
        out int width,
        out int height,
        out int left,
        out int top,
        out uint dpiX,
        out uint dpiY,
        out bool isPrimary);

    [DllImport("CaptureInterop.dll")]
    internal static extern bool CopyScreenshotPixels(
        IntPtr handle,
        byte[] buffer,
        int bufferSize);

    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool SaveScreenshotToPng(
        IntPtr handle,
        string filePath);

    [DllImport("CaptureInterop.dll")]
    internal static extern void FreeScreenshot(IntPtr handle);

    [DllImport("CaptureInterop.dll")]
    internal static extern IntPtr CombineScreenshots(
        IntPtr[] handles,
        int count);

    // Texture conversion for metadata scanners
    [DllImport("CaptureInterop.dll")]
    internal static extern bool ConvertTextureToPixelBuffer(
        IntPtr pTexture,
        IntPtr pDevice,
        IntPtr pContext,
        byte[] outBuffer,
        uint bufferSize,
        out uint outRowPitch);
}