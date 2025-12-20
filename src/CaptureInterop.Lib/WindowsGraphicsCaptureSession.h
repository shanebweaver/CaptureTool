#pragma once
#include "pch.h"
#include "ICaptureSession.h"
#include "MP4SinkWriter.h"

// Forward declarations
class FrameArrivedHandler;
class IAudioInputSource;
class MediaClock;

/// <summary>
/// Windows Graphics Capture API implementation of ICaptureSession.
/// Uses Windows.Graphics.Capture for screen recording with hardware acceleration.
/// Supports both video and audio capture with synchronized media streams.
/// </summary>
class WindowsGraphicsCaptureSession : public ICaptureSession
{
public:
    WindowsGraphicsCaptureSession();
    ~WindowsGraphicsCaptureSession() override;

    // Delete copy and move operations
    WindowsGraphicsCaptureSession(const WindowsGraphicsCaptureSession&) = delete;
    WindowsGraphicsCaptureSession& operator=(const WindowsGraphicsCaptureSession&) = delete;
    WindowsGraphicsCaptureSession(WindowsGraphicsCaptureSession&&) = delete;
    WindowsGraphicsCaptureSession& operator=(WindowsGraphicsCaptureSession&&) = delete;

    // ICaptureSession implementation
    bool Start(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio, HRESULT* outHr = nullptr) override;
    void Stop() override;
    void ToggleAudioCapture(bool enabled) override;
    bool IsActive() const override { return m_isActive; }

private:
    // Windows Graphics Capture resources
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_captureSession;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedEventToken;
    
    // Frame processing handler
    FrameArrivedHandler* m_frameHandler;
    
    // Media output
    MP4SinkWriter m_sinkWriter;
    
    // Audio capture
    std::unique_ptr<IAudioInputSource> m_audioInputSource;
    
    // Media clock for A/V synchronization (TODO: integrate properly)
    // std::unique_ptr<MediaClock> m_mediaClock;
    
    // Session state
    bool m_isActive;
};
