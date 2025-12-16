namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IScreenRecorder
{
    void PauseRecording();
    bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false);
    void StopRecording();
    void ToggleAudioCapture(bool enabled);
}
