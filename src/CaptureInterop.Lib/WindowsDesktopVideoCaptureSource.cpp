#include "pch.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "FrameArrivedHandler.h"
#include "WindowsGraphicsCaptureHelpers.h"
#include <strsafe.h>

using namespace WindowsGraphicsCaptureHelpers;

WindowsDesktopVideoCaptureSource::WindowsDesktopVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader)
    : m_config(config)
    , m_frameHandler(nullptr)
    , m_clockReader(clockReader)
    , m_width(0)
    , m_height(0)
    , m_isRunning(false)
{
    m_frameArrivedEventToken.value = 0;
    // Principle #6 (No Globals): Config and clock reader passed via constructor
}

WindowsDesktopVideoCaptureSource::~WindowsDesktopVideoCaptureSource()
{
    Stop();
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

    if (!m_roInitialized)
    {
        hr = RoInitialize(RO_INIT_MULTITHREADED);
        if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
        {
            if (outHr) *outHr = hr;
            return false;
        }
        if (SUCCEEDED(hr))
        {
            m_roInitialized = true;
        }
    }

    // Get the graphics capture item
    wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
    if (!interop)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    wil::com_ptr<IGraphicsCaptureItem> captureItem;
    if (m_config.targetType == CaptureTargetType::Window)
    {
        captureItem = GetGraphicsCaptureItemForWindow(m_config.hwnd, interop, &hr);
    }
    else
    {
        captureItem = GetGraphicsCaptureItemForMonitor(m_config.hMonitor, interop, &hr);
    }

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

    if (m_config.targetType == CaptureTargetType::Rectangle)
    {
        if (m_config.sourceLeft < 0 ||
            m_config.sourceTop < 0 ||
            m_config.sourceWidth == 0 ||
            m_config.sourceHeight == 0 ||
            static_cast<uint32_t>(m_config.sourceLeft) + m_config.sourceWidth > static_cast<uint32_t>(size.Width) ||
            static_cast<uint32_t>(m_config.sourceTop) + m_config.sourceHeight > static_cast<uint32_t>(size.Height))
        {
            if (outHr) *outHr = E_INVALIDARG;
            return false;
        }

        m_width = m_config.sourceWidth;
        m_height = m_config.sourceHeight;
    }
    else
    {
        m_width = size.Width;
        m_height = size.Height;
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
    m_frameArrivedEventToken = RegisterFrameArrivedHandler(m_framePool, m_callback, m_clockReader, &rawHandler, &hr);
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

void WindowsDesktopVideoCaptureSource::Stop()
{
    if (!m_isRunning && !m_frameHandler && !m_captureSession && !m_framePool && !m_context && !m_device)
    {
        return;
    }

    // Remove the event registration
    if (m_framePool)
    {
        m_framePool->remove_FrameArrived(m_frameArrivedEventToken);
    }

    m_frameArrivedEventToken.value = 0;

    // Stop the frame handler
    // Principle #5 (RAII Everything): wil::com_ptr automatically calls Release()
    if (m_frameHandler)
    {
        wchar_t message[192]{};
        (void)StringCchPrintfW(
            message,
            ARRAYSIZE(message),
            L"[CaptureInterop V1] Video frames received=%llu processed=%llu dropped=%llu\r\n",
            static_cast<unsigned long long>(m_frameHandler->GetReceivedFrameCount()),
            static_cast<unsigned long long>(m_frameHandler->GetProcessedFrameCount()),
            static_cast<unsigned long long>(m_frameHandler->GetDroppedFrameCount()));
        OutputDebugStringW(message);

        m_frameHandler->Stop();
        m_frameHandler.reset(); // Explicit reset for clarity, calls Release() automatically
    }

    ReleaseResources();

    m_isRunning = false;
}

void WindowsDesktopVideoCaptureSource::ReleaseResources()
{
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
            (void)closable->Close(); // Best-effort: ignore HRESULT if close fails
        }
        m_captureSession.reset();
    }

    // Release frame pool
    if (m_framePool)
    {
        wil::com_ptr<ABI::Windows::Foundation::IClosable> closable;
        if (SUCCEEDED(m_framePool->QueryInterface(IID_PPV_ARGS(closable.put()))))
        {
            (void)closable->Close(); // Best-effort: release frame-pool resources promptly
        }
        m_framePool.reset();
    }

    if (m_context)
    {
        m_context->ClearState();
        m_context->Flush();
    }

    m_context.reset();
    m_device.reset();

    if (m_roInitialized)
    {
        RoUninitialize();
        m_roInitialized = false;
    }
}
