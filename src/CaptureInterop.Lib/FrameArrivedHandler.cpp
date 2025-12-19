#include "pch.h"
#include "FrameArrivedHandler.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

FrameArrivedHandler::FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept
    : m_sinkWriter(std::move(sinkWriter)),
    m_ref(1),
    m_running(true),
    m_stopped(false),
    m_processingStarted(false)
{
    // Note: Thread is started after object is fully constructed
    // This prevents accessing uninitialized members in ProcessingThreadProc
}

void FrameArrivedHandler::StartProcessing()
{
    // Start background processing thread after object is fully constructed
    // Ensure only one thread is created (thread-safe)
    bool expected = false;
    if (m_processingStarted.compare_exchange_strong(expected, true))
    {
        m_processingThread = std::thread(&FrameArrivedHandler::ProcessingThreadProc, this);
        m_timerThread = std::thread(&FrameArrivedHandler::TimerThreadProc, this);
    }
}

FrameArrivedHandler::~FrameArrivedHandler()
{
    Stop();
}

void FrameArrivedHandler::Stop()
{
    // Make Stop() idempotent and thread-safe
    bool expected = false;
    if (!m_stopped.compare_exchange_strong(expected, true))
    {
        // Already stopped
        return;
    }
    
    m_running = false;
    m_queueCV.notify_one();
    
    if (m_processingThread.joinable())
    {
        m_processingThread.join();
    }
    
    if (m_timerThread.joinable())
    {
        m_timerThread.join();
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
            // We successfully set the first frame time
            firstFrameTime = timestamp.Duration;
        }
        else
        {
            // Another thread won the race, use their value
            firstFrameTime = m_firstFrameSystemTime.load();
        }
    }
    
    // Calculate relative timestamp
    LONGLONG relativeTimestamp = timestamp.Duration - firstFrameTime;

    // Update last frame tracking for duplicate frame generation
    // Use the same mutex for both texture and timestamp updates to avoid race conditions
    {
        std::lock_guard<std::mutex> lock(m_lastTextureMutex);
        m_lastTexture = texture;
        m_lastFrameTimestamp = relativeTimestamp;
        
        // Update next expected timestamp for 30 FPS (333333 ticks per frame)
        const LONGLONG FRAME_DURATION = 333333; // 100ns ticks for 30 FPS
        m_nextExpectedTimestamp = relativeTimestamp + FRAME_DURATION;
    }

    // Queue the frame for background processing instead of processing synchronously
    // This prevents blocking the event callback thread
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        
        // Maximum queue size to prevent unbounded memory growth
        const size_t MAX_QUEUE_SIZE = 30; // ~1 second at 30fps, matches 6 frame pool buffers
        if (m_frameQueue.size() < MAX_QUEUE_SIZE)
        {
            QueuedFrame queuedFrame;
            queuedFrame.texture = texture;
            queuedFrame.relativeTimestamp = relativeTimestamp;
            queuedFrame.isDuplicateFrame = false;
            m_frameQueue.push(std::move(queuedFrame));
            m_queueCV.notify_one();
        }
        else
        {
            // Queue is full - drop this frame to prevent blocking
            // This can happen when encoder is significantly slower than capture rate
            // Consider adding telemetry here if frame drops become problematic
        }
    }

    return S_OK;
}

void FrameArrivedHandler::ProcessingThreadProc()
{
    while (m_running)
    {
        QueuedFrame frame;
        wil::com_ptr<MP4SinkWriter> sinkWriter;
        
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
                
                // Capture a reference to sink writer while holding the lock
                // This ensures thread-safe access to m_sinkWriter
                // Note: m_sinkWriter lifetime is managed by the owner (ScreenRecorder)
                // and is guaranteed to outlive this handler's processing thread
                sinkWriter = m_sinkWriter;
            }
            else
            {
                continue;
            }
        }
        
        // Process the frame outside the lock
        if (frame.texture && sinkWriter)
        {
            HRESULT hr = sinkWriter->WriteFrame(frame.texture.get(), frame.relativeTimestamp);
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

void FrameArrivedHandler::TimerThreadProc()
{
    const LONGLONG FRAME_DURATION = 333333; // 100ns ticks for 30 FPS (1/30 second)
    const int SLEEP_MS = 33; // Sleep for ~33ms (30 FPS)
    const size_t MAX_QUEUE_SIZE = 30; // Match queue size constant from Invoke
    
    while (m_running)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(SLEEP_MS));
        
        if (!m_running)
        {
            break;
        }
        
        // Check if we have received any frames yet
        LONGLONG firstFrameTime = m_firstFrameSystemTime.load();
        if (firstFrameTime == 0)
        {
            // No frames yet, skip
            continue;
        }
        
        // Acquire lock for atomic read of last frame data
        wil::com_ptr<ID3D11Texture2D> lastTexture;
        LONGLONG nextExpected;
        {
            std::lock_guard<std::mutex> lock(m_lastTextureMutex);
            lastTexture = m_lastTexture;
            nextExpected = m_nextExpectedTimestamp;
        }
        
        // Generate duplicate frame if we have a texture and an expected timestamp
        if (lastTexture && nextExpected > 0)
        {
            // Generate duplicate frame at the next expected timestamp
            {
                std::lock_guard<std::mutex> lock(m_queueMutex);
                if (m_frameQueue.size() < MAX_QUEUE_SIZE)
                {
                    QueuedFrame queuedFrame;
                    queuedFrame.texture = lastTexture;
                    queuedFrame.relativeTimestamp = nextExpected;
                    queuedFrame.isDuplicateFrame = true;
                    m_frameQueue.push(std::move(queuedFrame));
                    m_queueCV.notify_one();
                }
            }
            
            // Update next expected timestamp for next iteration
            {
                std::lock_guard<std::mutex> lock(m_lastTextureMutex);
                m_nextExpectedTimestamp = nextExpected + FRAME_DURATION;
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
    
    // Start the processing thread after object is fully constructed
    handler->StartProcessing();
    
    HRESULT hr = framePool->add_FrameArrived(handler, &token);
    
    // Only provide handler reference to caller if registration succeeded
    if (SUCCEEDED(hr) && outHandler)
    {
        *outHandler = handler;
        handler->AddRef(); // Keep reference for caller
    }
    
    handler->Release(); // balance new
    if (outHr) *outHr = hr;
    return token;
}