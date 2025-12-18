using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    // Legacy 3-parameter method (for backward compatibility)
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false)
        => CaptureInterop.TryStartRecording(hMonitor, outputPath, captureAudio);
    
    // New 4-parameter method with microphone support
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureDesktopAudio, bool captureMicrophone)
        => CaptureInterop.TryStartRecordingWithMicrophone(hMonitor, outputPath, captureDesktopAudio, captureMicrophone);
    
    public void StopRecording() => CaptureInterop.TryStopRecording();

    public void PauseRecording() => CaptureInterop.TryPauseRecording();
    public void ResumeRecording() => CaptureInterop.TryResumeRecording();

    public void ToggleAudioCapture(bool enabled) => CaptureInterop.TryToggleAudioCapture(enabled);

    // Audio Mixer Configuration (Phase 3)
    public void SetAudioSourceTrack(int sourceId, int trackIndex) 
        => CaptureInterop.SetAudioSourceTrack(sourceId, trackIndex);

    public int GetAudioSourceTrack(int sourceId) 
        => CaptureInterop.GetAudioSourceTrack(sourceId);

    public void SetAudioSourceVolume(int sourceId, float volume) 
        => CaptureInterop.SetAudioSourceVolume(sourceId, volume);

    public float GetAudioSourceVolume(int sourceId) 
        => CaptureInterop.GetAudioSourceVolume(sourceId);

    public void SetAudioSourceMuted(int sourceId, bool muted) 
        => CaptureInterop.SetAudioSourceMuted(sourceId, muted);

    public bool IsAudioSourceMuted(int sourceId) 
        => CaptureInterop.IsAudioSourceMuted(sourceId);

    public void SetAudioTrackName(int trackIndex, string trackName) 
        => CaptureInterop.SetAudioTrackName(trackIndex, trackName);

    public void SetAudioMixingMode(bool mixedMode) 
        => CaptureInterop.SetAudioMixingMode(mixedMode);

    public bool GetAudioMixingMode() 
        => CaptureInterop.GetAudioMixingMode();

    // Encoder Pipeline Configuration (Phase 4)
    public void UseEncoderPipeline(bool enable) 
        => CaptureInterop.UseEncoderPipeline(enable);

    public bool IsEncoderPipelineEnabled() 
        => CaptureInterop.IsEncoderPipelineEnabled();

    public void SetVideoEncoderPreset(int preset) 
        => CaptureInterop.SetVideoEncoderPreset(preset);

    public int GetVideoEncoderPreset() 
        => CaptureInterop.GetVideoEncoderPreset();

    public void SetAudioEncoderQuality(int quality) 
        => CaptureInterop.SetAudioEncoderQuality(quality);

    public int GetAudioEncoderQuality() 
        => CaptureInterop.GetAudioEncoderQuality();

    public void EnableHardwareEncoding(bool enable) 
        => CaptureInterop.EnableHardwareEncoding(enable);

    public bool IsHardwareEncodingEnabled() 
        => CaptureInterop.IsHardwareEncodingEnabled();

}