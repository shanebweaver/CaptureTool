#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"

using namespace ABI::Windows::Foundation;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

class FrameArrivedHandler final
    : public ITypedEventHandler<Direct3D11CaptureFramePool*, IInspectable*>
{
public:

    explicit FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept :
        m_sinkWriter(sinkWriter),
        m_ref(1)
    {
    }

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
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

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return InterlockedIncrement(&m_ref);
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        ULONG ref = InterlockedDecrement(&m_ref);
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    // ITypedEventHandler
    HRESULT STDMETHODCALLTYPE Invoke(
        IDirect3D11CaptureFramePool* sender,
        IInspectable* /* args */) noexcept override
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

        wil::com_ptr<IDirect3DSurface> surface;
        hr = frame->get_Surface(surface.put());
        if (FAILED(hr) || !surface)
        {
            return hr;
        }

        // Not ABI
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

        return m_sinkWriter->WriteFrame(texture.get());
    }

private:
    volatile long m_ref;
    wil::com_ptr<MP4SinkWriter> m_sinkWriter;
};

static wil::com_ptr<IGraphicsCaptureItemInterop> GetGraphicsCaptureItemInterop(HRESULT* outHr = nullptr)
{
    wil::com_ptr<IGraphicsCaptureItemInterop> interop;

    HRESULT hr = RoInitialize(RO_INIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    HSTRING classId{};
    hr = WindowsCreateString(
        RuntimeClass_Windows_Graphics_Capture_GraphicsCaptureItem,
        static_cast<UINT32>(wcslen(RuntimeClass_Windows_Graphics_Capture_GraphicsCaptureItem)),
        &classId
    );
    if (FAILED(hr) || classId == 0)
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    hr = RoGetActivationFactory(
        classId,
        __uuidof(IGraphicsCaptureItemInterop),
        interop.put_void()
    );

    WindowsDeleteString(classId);

    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    if (outHr) *outHr = S_OK;
    return interop;
}

static wil::com_ptr<IGraphicsCaptureItem> GetGraphicsCaptureItemForMonitor(HMONITOR hMonitor, wil::com_ptr<IGraphicsCaptureItemInterop> interop, HRESULT* outHr = nullptr)
{
    wil::com_ptr<IGraphicsCaptureItem> item;
    HRESULT hr = interop->CreateForMonitor(
        hMonitor,
        __uuidof(IGraphicsCaptureItem),
        item.put_void()
    );

    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    if (outHr) *outHr = S_OK;
    return item;
}

struct D3DDeviceAndContext
{
    wil::com_ptr<ID3D11Device> device;
    wil::com_ptr<ID3D11DeviceContext> context;
};

static D3DDeviceAndContext InitializeD3D(HRESULT* outHr = nullptr)
{
    D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_0 };
    wil::com_ptr<ID3D11Device> device;
    wil::com_ptr<ID3D11DeviceContext> context;

    HRESULT hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        featureLevels,
        ARRAYSIZE(featureLevels),
        D3D11_SDK_VERSION,
        device.put(),
        nullptr,
        context.put()
    );

    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return {};
    }

    if (outHr) *outHr = hr;
    return { std::move(device), std::move(context) };
}

static wil::com_ptr<IDirect3DDevice> CreateDirect3DDevice(wil::com_ptr<ID3D11Device> device, HRESULT* outHr = nullptr)
{
    HRESULT hr;
    wil::com_ptr<IDXGIDevice> dxgiDevice;
    hr = device->QueryInterface(IID_PPV_ARGS(dxgiDevice.put()));
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    wil::com_ptr<IInspectable> direct3DDeviceInspectable;
    hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), direct3DDeviceInspectable.put());
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    wil::com_ptr<IDirect3DDevice> direct3DDevice;
    hr = direct3DDeviceInspectable->QueryInterface(IID_PPV_ARGS(direct3DDevice.put()));
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    if (outHr) *outHr = S_OK;
    return direct3DDevice;
}

static wil::com_ptr<IDirect3D11CaptureFramePool> CreateCaptureFramePool(
    wil::com_ptr<IGraphicsCaptureItem> captureItem,
    wil::com_ptr<IDirect3DDevice> direct3DDevice,
    HRESULT* outHr = nullptr)
{
    if (!captureItem || !direct3DDevice)
    {
        if (outHr) *outHr = E_POINTER;
        return nullptr;
    }

    HRESULT hr = S_OK;

    wil::com_ptr<IDirect3D11CaptureFramePoolStatics> factory;

    HSTRING className{};
    hr = WindowsCreateString(
        RuntimeClass_Windows_Graphics_Capture_Direct3D11CaptureFramePool,
        (UINT32)wcslen(RuntimeClass_Windows_Graphics_Capture_Direct3D11CaptureFramePool),
        &className);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    hr = RoGetActivationFactory(className, IID_PPV_ARGS(factory.put()));
    WindowsDeleteString(className);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    SizeInt32 size{};
    hr = captureItem->get_Size(&size);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    wil::com_ptr<IDirect3D11CaptureFramePool> framePool;

    hr = factory->Create(
        direct3DDevice.get(),
        DirectXPixelFormat_B8G8R8A8UIntNormalized,
        2, // number of buffers
        size,
        framePool.put());
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    if (outHr) *outHr = S_OK;
    return framePool;
}

static wil::com_ptr<IGraphicsCaptureSession> CreateCaptureSession(
    wil::com_ptr<IDirect3D11CaptureFramePool> framePool,
    wil::com_ptr<IGraphicsCaptureItem> captureItem, 
    HRESULT* outHr = nullptr)
{
    HRESULT hr = S_OK;
    wil::com_ptr<IGraphicsCaptureSession> session;
    hr = framePool->CreateCaptureSession(captureItem.get(), session.put());
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return nullptr;
    }

    return session;
}

static EventRegistrationToken RegisterFrameArrivedHandler(
    wil::com_ptr<IDirect3D11CaptureFramePool> framePool,
    wil::com_ptr<MP4SinkWriter> sinkWriter,
    HRESULT* outHr = nullptr)
{
    EventRegistrationToken token{};
    auto handler = new FrameArrivedHandler(sinkWriter);
    HRESULT hr = framePool->add_FrameArrived(handler, &token);
    handler->Release();
    if (outHr) *outHr = hr;
    return token;
}

static wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> g_session;
static wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> g_framePool;
static EventRegistrationToken g_frameArrivedEventToken;
static MP4SinkWriter g_sinkWriter;

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath)
    {
        HRESULT hr = S_OK;

        wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop(&hr);
        if (!interop)
        {
            return false;
        }

        wil::com_ptr<IGraphicsCaptureItem> captureItem = GetGraphicsCaptureItemForMonitor(hMonitor, interop, &hr);
        if (!captureItem)
        {
            return false;
        }

        D3DDeviceAndContext d3d = InitializeD3D(&hr);
        if (FAILED(hr))
        {
            return false;
        }

        wil::com_ptr<ID3D11Device> device = d3d.device;
        //wil::com_ptr<ID3D11DeviceContext> device = d3d.context;
        wil::com_ptr<IDirect3DDevice> abiDevice = CreateDirect3DDevice(device, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        g_framePool = CreateCaptureFramePool(captureItem, abiDevice, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        g_session = CreateCaptureSession(g_framePool, captureItem, &hr);
        if (FAILED(hr))
        {
            return false;
        }

        SizeInt32 size{};
        hr = captureItem->get_Size(&size);
        if (FAILED(hr)) return false;
        if (!g_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, &hr))
        {
            return false;
        }
        
        g_frameArrivedEventToken = RegisterFrameArrivedHandler(g_framePool, &g_sinkWriter , &hr);

        hr = g_session->StartCapture();
        if (FAILED(hr))
        {
            return false;
        }

        return true;
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryStopRecording()
    {
        if (g_framePool)
        {
            g_framePool->remove_FrameArrived(g_frameArrivedEventToken);
        }

        g_frameArrivedEventToken.value = 0;
        g_sinkWriter.Finalize();

        if (g_session)
        {
            g_session.reset();
        }

        if (g_framePool)
        {
            g_framePool.reset();
        }
    }
}