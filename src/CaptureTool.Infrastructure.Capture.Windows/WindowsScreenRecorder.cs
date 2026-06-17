using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    public CaptureRecorderResult StartRecording(CaptureRecordingOptions options)
    {
        var nativeOptions = new NativeCaptureRecordingOptions(options);
        return CaptureInterop.StartScreenRecording(in nativeOptions);
    }

    public CaptureRecorderResult StopRecording()
        => CaptureInterop.StopScreenRecording();

    public CaptureRecorderResult PauseRecording()
        => CaptureInterop.PauseScreenRecording();

    public CaptureRecorderResult ResumeRecording()
        => CaptureInterop.ResumeScreenRecording();

    public CaptureRecorderResult SetAudioCaptureEnabled(bool enabled)
        => CaptureInterop.SetScreenRecordingAudioEnabled(enabled ? 1u : 0u);

    public CaptureRecorderResult SetAudioInputSource(string? sourceId)
        => CaptureInterop.SetScreenRecordingAudioInputSource(sourceId);

    public CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback? callback)
        => CaptureInterop.RegisterVideoFrameCallback(callback);

    public CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback? callback)
        => CaptureInterop.RegisterAudioSampleCallback(callback);
}
