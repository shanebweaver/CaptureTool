#include "pch.h"
#include "FrameArrivedHandler.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

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

    // Get the frame's capture time (not processing time!)
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

    // Use SystemRelativeTime (actual capture time) for video timestamp
    // Store first frame time as reference, then calculate relative timestamps
    extern LONGLONG g_firstVideoSystemTime;
    
    if (g_firstVideoSystemTime == 0)
    {
        g_firstVideoSystemTime = timestamp.Duration;
    }
    
    // Calculate relative timestamp from first video frame
    // This gives us the actual time delta between frame captures
    LONGLONG relativeTimestamp = timestamp.Duration - g_firstVideoSystemTime;

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