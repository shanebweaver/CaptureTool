#pragma once
#include "IVideoCaptureSource.h"
#include "CaptureSessionConfig.h"
#include "pch.h"

// Forward declarations
class FrameArrivedHandler;
class IMediaClockReader;

/// <summary>
/// Windows Graphics Capture API implementation of IVideoCaptureSource.
/// Captures screen content using the Windows.Graphics.Capture API with hardware acceleration.
/// </summary>
class WindowsDesktopVideoCaptureSource : public IVideoCaptureSource
{
public:
    WindowsDesktopVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader);
    ~WindowsDesktopVideoCaptureSource() override;

    // Delete copy and move operations
    WindowsDesktopVideoCaptureSource(const WindowsDesktopVideoCaptureSource&) = delete;
    WindowsDesktopVideoCaptureSource& operator=(const WindowsDesktopVideoCaptureSource&) = delete;
    WindowsDesktopVideoCaptureSource(WindowsDesktopVideoCaptureSource&&) = delete;
    WindowsDesktopVideoCaptureSource& operator=(WindowsDesktopVideoCaptureSource&&) = delete;

    // IVideoCaptureSource implementation
    bool Initialize(HRESULT* outHr = nullptr) override;
    bool Start(HRESULT* outHr = nullptr) override;
    void Stop() override;
    UINT32 GetWidth() const override { return m_width; }
    UINT32 GetHeight() const override { return m_height; }
    void SetVideoFrameReadyCallback(VideoFrameReadyCallback callback) override { m_callback = callback; }
    bool IsRunning() const override { return m_isRunning; }

    /// <summary>
    /// Get the D3D11 device used for video capture.
    /// Available after successful initialization.
    /// </summary>
    ID3D11Device* GetDevice() const { return m_device.get(); }

private:
    // Configuration
    CaptureSessionConfig m_config;
    
    // Windows Graphics Capture resources
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_captureSession;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedEventToken;
    
    // D3D resources
    wil::com_ptr<ID3D11Device> m_device;
    wil::com_ptr<ID3D11DeviceContext> m_context;
    
    // Frame processing handler
    FrameArrivedHandler* m_frameHandler;
    
    // Callback
    VideoFrameReadyCallback m_callback;
    IMediaClockReader* m_clockReader;
    
    // Video dimensions
    UINT32 m_width;
    UINT32 m_height;
    
    // State
    bool m_isRunning;
};
