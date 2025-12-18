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

    // Audio Mixer Configuration (Phase 3)
    void SetAudioSourceTrack(int sourceId, int trackIndex);
    int GetAudioSourceTrack(int sourceId);
    void SetAudioSourceVolume(int sourceId, float volume);
    float GetAudioSourceVolume(int sourceId);
    void SetAudioSourceMuted(int sourceId, bool muted);
    bool IsAudioSourceMuted(int sourceId);
    void SetAudioTrackName(int trackIndex, string trackName);
    void SetAudioMixingMode(bool mixedMode);
    bool GetAudioMixingMode();

    // Encoder Pipeline Configuration (Phase 4)
    void UseEncoderPipeline(bool enable);
    bool IsEncoderPipelineEnabled();
    void SetVideoEncoderPreset(int preset);
    int GetVideoEncoderPreset();
    void SetAudioEncoderQuality(int quality);
    int GetAudioEncoderQuality();
    void EnableHardwareEncoding(bool enable);
    bool IsHardwareEncodingEnabled();
}
