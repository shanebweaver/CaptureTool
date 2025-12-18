#include "pch.h"
#include "ScreenCaptureSource.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;
using namespace ABI::Windows::Graphics::Capture;

ScreenCaptureSource::ScreenCaptureSource()
    : m_ref(1)
{
}

ScreenCaptureSource::~ScreenCaptureSource()
{
    Cleanup();
}

void ScreenCaptureSource::SetMonitor(HMONITOR hMonitor)
{
    m_hMonitor = hMonitor;
}

void ScreenCaptureSource::SetDevice(ID3D11Device* device)
{
    m_device = device;
}

void ScreenCaptureSource::GetResolution(UINT32& width, UINT32& height) const
{
    width = m_width;
    height = m_height;
}

void ScreenCaptureSource::SetFrameCallback(VideoFrameCallback callback)
{
    m_frameCallback = callback;
}

bool ScreenCaptureSource::Initialize()
{
    if (m_isInitialized)
    {
        return true; // Already initialized
    }

    if (!m_hMonitor || !m_device)
    {
        return false; // Configuration not set
    }

    HRESULT hr = S_OK;
    
    // Get Graphics.Capture interop
    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        return false;
    }

    // Get capture item for monitor
    m_captureItem = GetGraphicsCaptureItemForMonitor(m_hMonitor, interop, &hr);
    if (!m_captureItem)
    {
        return false;
    }

    // Create Direct3D device wrapper
    wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(m_device, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    // Create capture frame pool
    m_framePool = CreateCaptureFramePool(m_captureItem, abiDevice, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    // Create capture session
    m_session = CreateCaptureSession(m_framePool, m_captureItem, &hr);
    if (FAILED(hr))
    {
        return false;
    }

    // Get capture size
    SizeInt32 size{};
    hr = m_captureItem->get_Size(&size);
    if (FAILED(hr))
    {
        return false;
    }

    m_width = size.Width;
    m_height = size.Height;
    m_isInitialized = true;

    return true;
}

bool ScreenCaptureSource::Start()
{
    if (!m_isInitialized)
    {
        return false; // Must initialize first
    }

    if (m_isRunning)
    {
        return true; // Already running
    }

    if (!m_frameCallback)
    {
        return false; // No callback set
    }

    HRESULT hr = S_OK;

    // Create frame arrived handler with callback
    // Note: For Phase 1, we'll need to modify FrameArrivedHandler to accept a callback
    // For now, we'll register it with a wrapper that will be updated in Task 6
    m_frameArrivedToken = RegisterFrameArrivedHandler(m_framePool, nullptr, &m_frameHandler, &hr);
    if (FAILED(hr) || !m_frameHandler)
    {
        return false;
    }

    // Start the frame handler's processing thread
    m_frameHandler->StartProcessing();

    // Start capture
    hr = m_session->StartCapture();
    if (FAILED(hr))
    {
        // Cleanup on failure
        if (m_frameHandler)
        {
            m_frameHandler->Stop();
            m_frameHandler->Release();
            m_frameHandler = nullptr;
        }
        return false;
    }

    m_isRunning = true;
    return true;
}

void ScreenCaptureSource::Stop()
{
    if (!m_isRunning)
    {
        return; // Not running
    }

    // Remove event registration
    if (m_framePool)
    {
        m_framePool->remove_FrameArrived(m_frameArrivedToken);
    }
    m_frameArrivedToken.value = 0;

    // Stop frame handler
    if (m_frameHandler)
    {
        m_frameHandler->Stop();
        m_frameHandler->Release();
        m_frameHandler = nullptr;
    }

    m_isRunning = false;
}

bool ScreenCaptureSource::IsRunning() const
{
    return m_isRunning;
}

ULONG ScreenCaptureSource::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG ScreenCaptureSource::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}

void ScreenCaptureSource::Cleanup()
{
    Stop();
    
    if (m_session)
    {
        m_session.reset();
    }
    
    if (m_framePool)
    {
        m_framePool.reset();
    }
    
    if (m_captureItem)
    {
        m_captureItem.reset();
    }
}
