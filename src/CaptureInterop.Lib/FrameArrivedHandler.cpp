#include "pch.h"
#include "FrameArrivedHandler.h"
#include "IVideoCaptureSource.h"
#include "IMediaClockReader.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

FrameArrivedHandler::FrameArrivedHandler(
    VideoFrameReadyCallback callback,
    IMediaClockReader* clockReader,
    UINT32 cropLeft,
    UINT32 cropTop,
    UINT32 cropWidth,
    UINT32 cropHeight) noexcept
    : m_callback(std::move(callback)),
    m_clockReader(clockReader),
    m_cropLeft(cropLeft),
    m_cropTop(cropTop),
    m_cropWidth(cropWidth),
    m_cropHeight(cropHeight),
    m_ref(1),
    m_running(true),
    m_stopped(false),
    m_processingStarted(false)
{
    // Principle #6 (No Globals): Callback and clock reader passed via constructor
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
    }
}

FrameArrivedHandler::~FrameArrivedHandler()
{
    Stop();
    // Principle #5 (RAII Everything): Destructor ensures cleanup:
    // 1. Stop() joins background thread
    // 2. m_frameQueue cleanup: wil::com_ptr in QueuedFrame automatically Release() textures
    // 3. m_processingThread destructor (after join)
    // NOTE: We use manual ref counting (COM pattern) rather than smart pointers
    // because this is a COM event handler that must implement IUnknown.
}

void FrameArrivedHandler::Stop()
{
    bool expected = false;
    if (!m_stopped.compare_exchange_strong(expected, true))
    {
        return;
    }
    
    m_running = false;
    m_queueCV.notify_one();
    
    if (m_processingThread.joinable())
    {
        m_processingThread.join();
    }
    
    // Log any remaining frames as a warning
    std::lock_guard<std::mutex> lock(m_queueMutex);
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
        // COM pattern: Manual delete when ref count reaches 0
        // This is an exception to Principle #5 (RAII) due to COM requirements
        // The object manages its own lifetime through reference counting
    }

    return ref;
}

HRESULT STDMETHODCALLTYPE FrameArrivedHandler::Invoke(
    IDirect3D11CaptureFramePool* sender,
    IInspectable* /*args*/) noexcept
try
{
    if (!m_callback)
    {
        return E_POINTER;
    }

    wil::com_ptr<IDirect3D11CaptureFrame> frame;
    HRESULT hr = sender->TryGetNextFrame(frame.put());
    if (FAILED(hr) || !frame)
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

    LONGLONG timestamp = 0;
    if (m_clockReader && m_clockReader->IsRunning())
    {
        timestamp = m_clockReader->GetCurrentTime();
    }
    
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        
        // Keep queue size at 3 frames to reduce memory pressure while providing minimal buffering
        // Reduced from 6 to minimize memory accumulation during encoding delays
        if (m_frameQueue.size() < 3)
        {
            QueuedFrame queuedFrame;
            queuedFrame.texture = texture;
            queuedFrame.relativeTimestamp = timestamp;
            m_frameQueue.push(std::move(queuedFrame));
            m_queueCV.notify_one();
        }
        else
        {
            // Queue full - drop frame to prevent memory buildup
            // Increment dropped frame counter
            m_droppedFrameCount.fetch_add(1, std::memory_order_relaxed);
            
            // Explicitly reset texture to encourage immediate GPU memory release
            texture.reset();
        }
    }

    return S_OK;
}
catch (...)
{
    m_processingResult.store(E_FAIL, std::memory_order_release);
    return E_FAIL;
}

void FrameArrivedHandler::ProcessingThreadProc()
{
    try
    {
        while (m_running)
        {
            QueuedFrame frame;

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

            if (frame.texture && m_callback && m_running)
            {
                wil::com_ptr<ID3D11Texture2D> croppedTexture;
                HRESULT cropResult = CreateCroppedTexture(frame.texture.get(), croppedTexture);
                if (FAILED(cropResult))
                {
                    m_processingResult.store(cropResult, std::memory_order_release);
                    m_running.store(false, std::memory_order_release);
                    break;
                }

                VideoFrameReadyEventArgs args;
                args.pTexture = croppedTexture ? croppedTexture.get() : frame.texture.get();
                args.timestamp = frame.relativeTimestamp;

                m_callback(args);
            }
        }
    }
    catch (...)
    {
        m_processingResult.store(E_FAIL, std::memory_order_release);
        m_running.store(false, std::memory_order_release);
    }
}

HRESULT FrameArrivedHandler::CreateCroppedTexture(
    ID3D11Texture2D* source,
    wil::com_ptr<ID3D11Texture2D>& croppedTexture)
{
    if (!source)
    {
        return E_INVALIDARG;
    }

    D3D11_TEXTURE2D_DESC sourceDesc{};
    source->GetDesc(&sourceDesc);

    if (m_cropLeft == 0 &&
        m_cropTop == 0 &&
        m_cropWidth == sourceDesc.Width &&
        m_cropHeight == sourceDesc.Height)
    {
        return S_OK;
    }

    if (m_cropWidth == 0 ||
        m_cropHeight == 0 ||
        m_cropWidth > sourceDesc.Width ||
        m_cropHeight > sourceDesc.Height ||
        m_cropLeft > sourceDesc.Width - m_cropWidth ||
        m_cropTop > sourceDesc.Height - m_cropHeight)
    {
        return E_INVALIDARG;
    }

    wil::com_ptr<ID3D11Device> device;
    source->GetDevice(device.put());
    if (!device)
    {
        return E_FAIL;
    }

    if (!m_croppedTexture)
    {
        D3D11_TEXTURE2D_DESC croppedDesc = sourceDesc;
        croppedDesc.Width = m_cropWidth;
        croppedDesc.Height = m_cropHeight;

        HRESULT hr = device->CreateTexture2D(
            &croppedDesc,
            nullptr,
            m_croppedTexture.put());
        if (FAILED(hr))
        {
            return hr;
        }
    }

    wil::com_ptr<ID3D11DeviceContext> context;
    device->GetImmediateContext(context.put());
    if (!context)
    {
        return E_FAIL;
    }

    D3D11_BOX sourceBox
    {
        m_cropLeft,
        m_cropTop,
        0,
        m_cropLeft + m_cropWidth,
        m_cropTop + m_cropHeight,
        1
    };
    context->CopySubresourceRegion(
        m_croppedTexture.get(),
        0,
        0,
        0,
        0,
        source,
        0,
        &sourceBox);

    croppedTexture = m_croppedTexture;
    return S_OK;
}

EventRegistrationToken RegisterFrameArrivedHandler(
    wil::com_ptr<IDirect3D11CaptureFramePool> framePool,
    VideoFrameReadyCallback callback,
    IMediaClockReader* clockReader,
    UINT32 cropLeft,
    UINT32 cropTop,
    UINT32 cropWidth,
    UINT32 cropHeight,
    FrameArrivedHandler** outHandler,
    HRESULT* outHr)
{
    EventRegistrationToken token{};
    auto handler = new FrameArrivedHandler(
        callback,
        clockReader,
        cropLeft,
        cropTop,
        cropWidth,
        cropHeight);
    
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
