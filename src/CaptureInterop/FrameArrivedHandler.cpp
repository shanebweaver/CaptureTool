#include "pch.h"
#include "FrameArrivedHandler.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

// Access to global pause state from ScreenRecorder.cpp
extern std::atomic<bool> g_isPaused;
extern std::atomic<LONGLONG> g_totalPausedDuration;

FrameArrivedHandler::FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept
    : m_sinkWriter(std::move(sinkWriter)),
    m_ref(1)
{
}

HRESULT STDMETHODCALLTYPE FrameArrivedHandler::QueryInterface(REFIID riid, void** ppvObject)
{
    if (!ppvObject)
    {
        return E_POINTER;
    }
        
    *ppvObject = nullptr;
        
    if (riid == __uuidof(IUnknown) ||
        riid == __uuidof(ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>))
    {
        *ppvObject = static_cast<ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>*>(this);
        AddRef();
        return S_OK;
    }
        
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE FrameArrivedHandler::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG STDMETHODCALLTYPE FrameArrivedHandler::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }

    return ref;
}

HRESULT STDMETHODCALLTYPE FrameArrivedHandler::Invoke(IDirect3D11CaptureFramePool* sender, IInspectable* /*args*/) noexcept
{
    if (!m_sinkWriter)
    {
        return E_POINTER;
    }

    wil::com_ptr<IDirect3D11CaptureFrame> frame;
    HRESULT hr = sender->TryGetNextFrame(frame.put());
    if (FAILED(hr) || !frame)
    {
        return hr;
    }

    // Check if recording is paused
    if (g_isPaused)
    {
        // Skip writing frame when paused, but continue receiving frames
        return S_OK;
    }

    TimeSpan timestamp{};
    hr = frame->get_SystemRelativeTime(&timestamp);
    if (FAILED(hr))
    {
        return hr;
    }

    wil::com_ptr<IDirect3DSurface> surface;
    hr = frame->get_Surface(surface.put());
    if (FAILED(hr) || !surface)
    {
        return hr;
    }

    wil::com_ptr<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess> access;
    hr = surface->QueryInterface(IID_PPV_ARGS(&access));
    if (FAILED(hr) || !access)
    {
        return hr;
    }

    wil::com_ptr<ID3D11Texture2D> texture;
    hr = access->GetInterface(IID_PPV_ARGS(&texture));
    if (FAILED(hr) || !texture)
    {
        return hr;
    }

    // Phase 3: Use common QPC-based time base for synchronization
    static LONGLONG firstFrameSystemTime = 0;
    static LARGE_INTEGER qpcFrequency = {};
    
    if (firstFrameSystemTime == 0)
    {
        // First frame - establish the time base
        firstFrameSystemTime = timestamp.Duration;
        
        // Initialize QPC frequency for timestamp conversion
        QueryPerformanceFrequency(&qpcFrequency);
        
        // Set the recording start time on the sink writer for audio synchronization
        // We use the current QPC time as the common start point
        LARGE_INTEGER qpc;
        QueryPerformanceCounter(&qpc);
        m_sinkWriter->SetRecordingStartTime(qpc.QuadPart);
    }
    
    // Calculate relative timestamp in 100-nanosecond units
    // Frame's SystemRelativeTime is already in 100ns units, so we just need the elapsed time
    LONGLONG relativeTimestamp = timestamp.Duration - firstFrameSystemTime;
    
    // Adjust timestamp by subtracting total paused duration
    // Convert QPC ticks to 100-nanosecond units
    LONGLONG totalPausedDuration = g_totalPausedDuration.load();
    if (totalPausedDuration > 0 && qpcFrequency.QuadPart > 0)
    {
        LONGLONG pausedDuration100ns = (totalPausedDuration * 10000000LL) / qpcFrequency.QuadPart;
        relativeTimestamp -= pausedDuration100ns;
        
        // Ensure timestamp doesn't go negative
        if (relativeTimestamp < 0)
        {
            relativeTimestamp = 0;
        }
    }

    return m_sinkWriter->WriteFrame(texture.get(), relativeTimestamp);
}

EventRegistrationToken RegisterFrameArrivedHandler(
    wil::com_ptr<IDirect3D11CaptureFramePool> framePool,
    wil::com_ptr<MP4SinkWriter> sinkWriter,
    HRESULT* outHr)
{
    EventRegistrationToken token{};
    auto handler = new FrameArrivedHandler(sinkWriter);
    HRESULT hr = framePool->add_FrameArrived(handler, &token);
    handler->Release(); // balance new
    if (outHr) *outHr = hr;
    return token;
}