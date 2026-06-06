#include "pch.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "FrameArrivedHandler.h"
#include "WindowsGraphicsCaptureHelpers.h"

#include <algorithm>

using namespace WindowsGraphicsCaptureHelpers;

WindowsDesktopVideoCaptureSource::WindowsDesktopVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader)
    : m_config(config)
    , m_frameHandler(nullptr)
    , m_clockReader(clockReader)
    , m_width(0)
    , m_height(0)
    , m_cropLeft(0)
    , m_cropTop(0)
    , m_isRunning(false)
{
    m_frameArrivedEventToken.value = 0;
    // Principle #6 (No Globals): Config and clock reader passed via constructor
}

WindowsDesktopVideoCaptureSource::~WindowsDesktopVideoCaptureSource()
{
    (void)Stop();
    // Principle #5 (RAII Everything): Destructor ensures cleanup via following chain:
    // 1. Stop() unregisters event handler, stops frame processing, releases resources
    // 2. wil::com_ptr members automatically Release() COM objects:
    //    - m_frameHandler (if any)
    //    - m_context, m_device (D3D resources)
    //    - m_framePool, m_captureSession (Windows Graphics Capture resources)
    // No manual Release() calls needed - type system guarantees cleanup.
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

    if (size.Width <= 0 || size.Height <= 0)
    {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }

    UINT32 sourceWidth = static_cast<UINT32>(size.Width);
    UINT32 sourceHeight = static_cast<UINT32>(size.Height);
    m_cropLeft = 0;
    m_cropTop = 0;
    m_width = sourceWidth;
    m_height = sourceHeight;

    if (m_config.HasCaptureArea())
    {
        LONG left = std::clamp<LONG>(m_config.captureArea.left, 0, size.Width);
        LONG top = std::clamp<LONG>(m_config.captureArea.top, 0, size.Height);
        LONG right = std::clamp<LONG>(m_config.captureArea.right, left, size.Width);
        LONG bottom = std::clamp<LONG>(m_config.captureArea.bottom, top, size.Height);

        UINT32 width = static_cast<UINT32>(right - left);
        UINT32 height = static_cast<UINT32>(bottom - top);

        // The Media Foundation H.264 encoder requires even frame dimensions.
        width &= ~1u;
        height &= ~1u;
        if (width < 2 || height < 2)
        {
            if (outHr) *outHr = E_INVALIDARG;
            return false;
        }

        m_cropLeft = static_cast<UINT32>(left);
        m_cropTop = static_cast<UINT32>(top);
        m_width = width;
        m_height = height;
    }

    if (outHr) *outHr = S_OK;
    return true;
}

bool WindowsDesktopVideoCaptureSource::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    if (!m_callback)
    {
        if (outHr) *outHr = E_POINTER;
        return false;
    }

    if (m_isRunning)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Register frame arrived handler with callback and clock reader for timestamps
    // Principle #3 (No Nullable Pointers): Use wil::com_ptr to manage handler lifetime
    FrameArrivedHandler* rawHandler = nullptr;
    m_frameArrivedEventToken = RegisterFrameArrivedHandler(
        m_framePool,
        m_callback,
        m_clockReader,
        m_cropLeft,
        m_cropTop,
        m_width,
        m_height,
        &rawHandler,
        &hr);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Transfer ownership to wil::com_ptr (takes over the AddRef from RegisterFrameArrivedHandler)
    // Only attach if registration succeeded (rawHandler will be non-null)
    if (rawHandler)
    {
        m_frameHandler.attach(rawHandler);
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

HRESULT WindowsDesktopVideoCaptureSource::Stop()
{
    HRESULT firstError = S_OK;
    auto retainFirstError = [&firstError](HRESULT hr)
    {
        if (FAILED(hr) && SUCCEEDED(firstError))
        {
            firstError = hr;
        }
    };

    // Remove the event registration
    if (m_framePool && m_frameArrivedEventToken.value != 0)
    {
        retainFirstError(m_framePool->remove_FrameArrived(m_frameArrivedEventToken));
    }

    m_frameArrivedEventToken.value = 0;

    // Stop the frame handler
    // Principle #5 (RAII Everything): wil::com_ptr automatically calls Release()
    if (m_frameHandler)
    {
        m_frameHandler->Stop();
        retainFirstError(m_frameHandler->GetProcessingResult());
        m_frameHandler.reset(); // Explicit reset for clarity, calls Release() automatically
    }

    // Release capture session
    // Principle #3 (No Nullable Pointers): wil::com_ptr handles Release() automatically
    if (m_captureSession)
    {
        // Explicitly close the session via IClosable before releasing the reference.
        // The WinRT GraphicsCaptureSession destructor calls Close() -> StopCapture()
        // internally. If StopCapture() fails (e.g., the display was disconnected),
        // the WinRT layer converts the failure into a C++ exception via
        // winrt::check_hresult, which propagates out of the destructor and crashes.
        // Calling Close() here via the ABI IClosable interface (which returns HRESULT
        // instead of throwing) ensures the session is already closed before reset()
        // releases the last reference, preventing the destructor from calling
        // StopCapture() in an unknown state.
        wil::com_ptr<ABI::Windows::Foundation::IClosable> closable;
        if (SUCCEEDED(m_captureSession->QueryInterface(IID_PPV_ARGS(closable.put()))))
        {
            retainFirstError(closable->Close());
        }
        m_captureSession.reset();
    }

    // Close and release the frame pool explicitly. Like the capture session,
    // the frame pool is IClosable and can hold capture resources until Close.
    if (m_framePool)
    {
        wil::com_ptr<ABI::Windows::Foundation::IClosable> closable;
        if (SUCCEEDED(m_framePool->QueryInterface(IID_PPV_ARGS(closable.put()))))
        {
            retainFirstError(closable->Close());
        }
        m_framePool.reset();
    }

    m_isRunning = false;
    return firstError;
}
