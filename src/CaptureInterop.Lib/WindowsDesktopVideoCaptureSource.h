#pragma once
#include "IVideoCaptureSource.h"
#include "CaptureSessionConfig.h"
#include "pch.h"

#include <d3d11.h>
#include <Windows.h>
#include <EventToken.h>
#include <windows.graphics.capture.h>
#include <wil/com.h>

// Forward declarations
class FrameArrivedHandler;
class IMediaClockReader;

/// <summary>
/// Windows Graphics Capture API implementation of IVideoCaptureSource.
/// Captures screen content using the Windows.Graphics.Capture API with hardware acceleration.
/// 
/// Implements Rust Principles:
/// - Principle #3 (No Nullable Pointers): Uses wil::com_ptr for all COM object lifetime management,
///   including m_frameHandler. No raw pointers that need manual Release() calls.
/// - Principle #5 (RAII Everything): Destructor calls Stop() to release all resources.
///   All COM objects use wil::com_ptr for automatic Release() on destruction.
/// - Principle #6 (No Globals): Clock reader passed via constructor, config is a value type
/// - Principle #8 (Thread Safety by Design): Frame callbacks are invoked on background thread
/// 
/// Ownership model:
/// - Owns D3D device and context via wil::com_ptr
/// - Owns capture session and frame pool via wil::com_ptr
/// - Owns frame handler via wil::com_ptr (improved from raw pointer with manual ref counting)
/// 
/// Threading model:
/// - Initialize/Start/Stop called from session thread
/// - Frame callbacks invoked from FrameArrivedHandler's background processing thread
/// - m_isRunning flag is not atomic (single-threaded control access assumed)
/// 
/// See docs/RUST_PRINCIPLES.md for more details.
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
    wil::com_ptr<FrameArrivedHandler> m_frameHandler;
    
    // Callback
    VideoFrameReadyCallback m_callback;
    IMediaClockReader* m_clockReader;
    
    // Video dimensions
    UINT32 m_width;
    UINT32 m_height;
    
    // State
    bool m_isRunning;
};
