#pragma once
#include "IVideoSource.h"
#include "FrameArrivedHandler.h"
#include <wil/com.h>

// Forward declarations
namespace ABI {
    namespace Windows {
        namespace Graphics {
            namespace Capture {
                interface IGraphicsCaptureItem;
                interface IGraphicsCaptureSession;
                interface IDirect3D11CaptureFramePool;
            }
        }
    }
}

/// <summary>
/// Video source implementation for screen capture using Windows.Graphics.Capture API.
/// Encapsulates all screen capture logic in a reusable, callback-based source.
/// </summary>
class ScreenCaptureSource : public IVideoSource
{
public:
    ScreenCaptureSource();
    ~ScreenCaptureSource();

    // Configuration (must be called before Initialize)
    /// <summary>
    /// Set the monitor to capture from.
    /// Must be called before Initialize().
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    void SetMonitor(HMONITOR hMonitor);
    
    /// <summary>
    /// Set the D3D11 device to use for capture.
    /// Must be called before Initialize().
    /// </summary>
    /// <param name="device">Pointer to ID3D11Device.</param>
    void SetDevice(ID3D11Device* device);

    // IVideoSource implementation
    void GetResolution(UINT32& width, UINT32& height) const override;
    void SetFrameCallback(VideoFrameCallback callback) override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    // Reference counting
    volatile long m_ref = 1;

    // Configuration
    HMONITOR m_hMonitor = nullptr;
    ID3D11Device* m_device = nullptr;

    // Capture infrastructure
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem> m_captureItem;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_session;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedToken;
    
    // Frame handler (modified to use callback instead of direct MP4SinkWriter reference)
    FrameArrivedHandler* m_frameHandler = nullptr;
    VideoFrameCallback m_frameCallback;
    
    // Properties
    UINT32 m_width = 0;
    UINT32 m_height = 0;
    bool m_isRunning = false;
    bool m_isInitialized = false;

    // Helper methods
    bool InitializeGraphicsCapture(HRESULT* outHr);
    void Cleanup();
};
