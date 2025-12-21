#include "pch.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "FrameArrivedHandler.h"
#include "MP4SinkWriter.h"
#include "WindowsGraphicsCaptureHelpers.h"

using namespace WindowsGraphicsCaptureHelpers;

WindowsDesktopVideoCaptureSource::WindowsDesktopVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader)
    : m_config(config)
    , m_frameHandler(nullptr)
    , m_sinkWriter(nullptr)
    , m_clockReader(clockReader)
    , m_width(0)
    , m_height(0)
    , m_isRunning(false)
{
    m_frameArrivedEventToken.value = 0;
}

WindowsDesktopVideoCaptureSource::~WindowsDesktopVideoCaptureSource()
{
    Stop();
}

bool WindowsDesktopVideoCaptureSource::Initialize(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Get the graphics capture item
    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    wil::com_ptr<IGraphicsCaptureItem> captureItem = GetGraphicsCaptureItemForMonitor(m_config.hMonitor, interop, &hr);
    if (!captureItem)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize D3D device
    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    m_device = d3d.device;
    m_context = d3d.context;

    wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(m_device, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Create frame pool
    m_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Create capture session
    m_captureSession = CreateCaptureSession(m_framePool, captureItem, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get capture item size
    SizeInt32 size{};
    hr = captureItem->get_Size(&size);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    m_width = size.Width;
    m_height = size.Height;

    if (outHr) *outHr = S_OK;
    return true;
}

bool WindowsDesktopVideoCaptureSource::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    if (!m_sinkWriter)
    {
        if (outHr) *outHr = E_POINTER;
        return false;
    }

    if (m_isRunning)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Register frame arrived handler with clock reader for timestamps
    wil::com_ptr<MP4SinkWriter> sinkWriterPtr(m_sinkWriter);
    m_frameArrivedEventToken = RegisterFrameArrivedHandler(m_framePool, sinkWriterPtr, m_clockReader, &m_frameHandler, &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Start video capture
    hr = m_captureSession->StartCapture();
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    m_isRunning = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsDesktopVideoCaptureSource::Stop()
{
    if (!m_isRunning)
    {
        return;
    }

    // Remove the event registration
    if (m_framePool)
    {
        m_framePool->remove_FrameArrived(m_frameArrivedEventToken);
    }

    m_frameArrivedEventToken.value = 0;

    // Stop the frame handler and release our reference
    if (m_frameHandler)
    {
        m_frameHandler->Stop();
        m_frameHandler->Release();
        m_frameHandler = nullptr;
    }

    // Release capture session
    if (m_captureSession)
    {
        m_captureSession.reset();
    }

    // Release frame pool
    if (m_framePool)
    {
        m_framePool.reset();
    }

    m_isRunning = false;
}
