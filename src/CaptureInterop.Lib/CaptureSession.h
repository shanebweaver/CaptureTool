#pragma once
#include "pch.h"
#include "MP4SinkWriter.h"

// Forward declarations
class FrameArrivedHandler;
class AudioCaptureHandler;

/// <summary>
/// Encapsulates a single capture session including audio and video capture.
/// Manages the lifetime of capture resources, frame pool, and sink writer.
/// </summary>
class CaptureSession
{
public:
    CaptureSession();
    ~CaptureSession();

    // Delete copy and move operations
    CaptureSession(const CaptureSession&) = delete;
    CaptureSession& operator=(const CaptureSession&) = delete;
    CaptureSession(CaptureSession&&) = delete;
    CaptureSession& operator=(CaptureSession&&) = delete;

    /// <summary>
    /// Initialize and start the capture session.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="captureAudio">Whether to capture system audio.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if session started successfully, false otherwise.</returns>
    bool Start(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio, HRESULT* outHr = nullptr);

    /// <summary>
    /// Stop the capture session and finalize the output file.
    /// </summary>
    void Stop();

    /// <summary>
    /// Toggle audio capture on/off during recording.
    /// </summary>
    /// <param name="enabled">True to enable audio, false to mute.</param>
    void ToggleAudioCapture(bool enabled);

    /// <summary>
    /// Check if the session is currently active.
    /// </summary>
    /// <returns>True if session is running, false otherwise.</returns>
    bool IsActive() const { return m_isActive; }

private:
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_captureSession;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedEventToken;
    FrameArrivedHandler* m_frameHandler;
    MP4SinkWriter m_sinkWriter;
    std::unique_ptr<AudioCaptureHandler> m_audioHandler;
    bool m_isActive;
};
