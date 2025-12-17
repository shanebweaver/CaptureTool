namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IScreenRecorder
{
    bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false);
    void StopRecording();
    void PauseRecording();
    void ResumeRecording();
    void ToggleAudioCapture(bool enabled);
}
