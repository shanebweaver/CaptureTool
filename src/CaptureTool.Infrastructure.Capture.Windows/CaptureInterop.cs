using CaptureTool.Domain.Capture;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows;

internal static partial class CaptureInterop
{
    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult StartScreenRecording(in NativeCaptureRecordingOptions options);

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult PauseScreenRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult ResumeScreenRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult StopScreenRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult SetScreenRecordingAudioEnabled(uint enabled);

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback? callback);

    [DllImport("CaptureInterop.dll")]
    internal static extern CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback? callback);

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

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeCaptureRecordingOptions
{
    public NativeCaptureRecordingOptions(CaptureRecordingOptions options)
    {
        TargetKind = options.Target.Kind;
        MonitorHandle = options.Target.MonitorHandle;
        WindowHandle = options.Target.WindowHandle;
        Left = options.Target.Left;
        Top = options.Target.Top;
        Width = options.Target.Width;
        Height = options.Target.Height;
        OutputPath = options.OutputPath;
        CaptureAudio = options.CaptureAudio ? 1u : 0u;
        FrameRate = options.FrameRate;
        VideoBitrate = options.VideoBitrate;
        AudioBitrate = options.AudioBitrate;
    }

    public readonly CaptureRecordingTargetKind TargetKind;
    public readonly nint MonitorHandle;
    public readonly nint WindowHandle;
    public readonly int Left;
    public readonly int Top;
    public readonly int Width;
    public readonly int Height;

    [MarshalAs(UnmanagedType.LPWStr)]
    public readonly string OutputPath;

    public readonly uint CaptureAudio;
    public readonly uint FrameRate;
    public readonly uint VideoBitrate;
    public readonly uint AudioBitrate;
}
