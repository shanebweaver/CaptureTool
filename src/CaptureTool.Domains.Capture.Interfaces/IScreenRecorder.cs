namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IScreenRecorder
{
    // Legacy 3-parameter method (for backward compatibility)
    bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false);
    
    // New 4-parameter method with microphone support
    bool StartRecording(nint hMonitor, string outputPath, bool captureDesktopAudio, bool captureMicrophone);
    
    void StopRecording();
    void PauseRecording();
    void ResumeRecording();
    void ToggleAudioCapture(bool enabled);
}
