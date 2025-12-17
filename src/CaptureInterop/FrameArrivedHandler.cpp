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
    // Start background processing thread
    m_processingThread = std::thread(&FrameArrivedHandler::ProcessingThreadProc, this);
}

FrameArrivedHandler::~FrameArrivedHandler()
{
    Stop();
}

void FrameArrivedHandler::Stop()
{
    m_running = false;
    m_queueCV.notify_one();
    
    if (m_processingThread.joinable())
    {
        m_processingThread.join();
    }
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

    // First frame - establish the time base
    LONGLONG firstFrameTime = m_firstFrameSystemTime.load();
    if (firstFrameTime == 0)
    {
        // Try to set it atomically
        LONGLONG expected = 0;
        if (m_firstFrameSystemTime.compare_exchange_strong(expected, timestamp.Duration))
        {
            // We successfully set it - also set recording start time
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            m_sinkWriter->SetRecordingStartTime(qpc.QuadPart);
        }
        firstFrameTime = m_firstFrameSystemTime.load();
    }
    
    // Calculate relative timestamp
    LONGLONG relativeTimestamp = timestamp.Duration - firstFrameTime;

    // Queue the frame for background processing instead of processing synchronously
    // This prevents blocking the event callback thread
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        
        // Limit queue size to prevent memory buildup
        if (m_frameQueue.size() < 10)
        {
            QueuedFrame queuedFrame;
            queuedFrame.texture = texture;
            queuedFrame.relativeTimestamp = relativeTimestamp;
            m_frameQueue.push(std::move(queuedFrame));
            m_queueCV.notify_one();
        }
        // If queue is full, drop this frame (better than blocking the callback)
    }

    return S_OK;
}

void FrameArrivedHandler::ProcessingThreadProc()
{
    while (m_running)
    {
        QueuedFrame frame;
        
        // Wait for a frame to process
        {
            std::unique_lock<std::mutex> lock(m_queueMutex);
            m_queueCV.wait(lock, [this] { return !m_frameQueue.empty() || !m_running; });
            
            if (!m_running && m_frameQueue.empty())
            {
                break;
            }
            
            if (!m_frameQueue.empty())
            {
                frame = std::move(m_frameQueue.front());
                m_frameQueue.pop();
            }
            else
            {
                continue;
            }
        }
        
        // Process the frame outside the lock
        if (frame.texture && m_sinkWriter)
        {
            HRESULT hr = m_sinkWriter->WriteFrame(frame.texture.get(), frame.relativeTimestamp);
            // If write fails, continue processing (don't stop the thread)
            // The sink writer will handle errors internally
            if (FAILED(hr))
            {
                // Frame write failed, but continue processing queue
                // This prevents one bad frame from stopping the entire recording
            }
        }
    }
}

EventRegistrationToken RegisterFrameArrivedHandler(
    wil::com_ptr<IDirect3D11CaptureFramePool> framePool,
    wil::com_ptr<MP4SinkWriter> sinkWriter,
    FrameArrivedHandler** outHandler,
    HRESULT* outHr)
{
    EventRegistrationToken token{};
    auto handler = new FrameArrivedHandler(sinkWriter);
    HRESULT hr = framePool->add_FrameArrived(handler, &token);
    
    // Store handler reference if requested (for cleanup)
    if (outHandler)
    {
        *outHandler = handler;
        handler->AddRef(); // Keep reference for caller
    }
    
    handler->Release(); // balance new
    if (outHr) *outHr = hr;
    return token;
}