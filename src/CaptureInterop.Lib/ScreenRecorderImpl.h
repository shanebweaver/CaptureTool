#pragma once
#include "pch.h"
#include "MP4SinkWriter.h"

// Forward declarations
class FrameArrivedHandler;
class AudioCaptureHandler;

/// <summary>
/// Implementation class for screen recording functionality.
/// Manages the capture session, frame pool, and audio/video sink writers.
/// </summary>
class ScreenRecorderImpl
{
public:
    ScreenRecorderImpl();
    ~ScreenRecorderImpl();

    /// <summary>
    /// Start recording the specified monitor to an output file.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="captureAudio">Whether to capture system audio.</param>
    /// <returns>True if recording started successfully, false otherwise.</returns>
    bool StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio);

    /// <summary>
    /// Pause the current recording.
    /// </summary>
    void PauseRecording();

    /// <summary>
    /// Resume the paused recording.
    /// </summary>
    void ResumeRecording();

    /// <summary>
    /// Stop the current recording and finalize the output file.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Toggle audio capture on/off during recording.
    /// </summary>
    /// <param name="enabled">True to enable audio, false to mute.</param>
    void ToggleAudioCapture(bool enabled);

private:
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_session;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedEventToken;
    FrameArrivedHandler* m_frameHandler;
    MP4SinkWriter m_sinkWriter;
    std::unique_ptr<AudioCaptureHandler> m_audioHandler;
};
