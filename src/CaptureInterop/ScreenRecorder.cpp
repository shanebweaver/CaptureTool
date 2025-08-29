#include "pch.h"
#include "ScreenRecorder.h"

#include <wil/com.h>

#include <windows.graphics.capture.h>
#include <windows.graphics.capture.interop.h>

#include <roapi.h>
#include <d3d11.h>

using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;
using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;

// Globals (later we can wrap in a struct)
static wil::com_ptr<IGraphicsCaptureItem> g_captureItem;
static wil::com_ptr<ID3D11Device> g_d3dDevice;
static wil::com_ptr<ID3D11DeviceContext> g_d3dContext;
static wil::com_ptr<IGraphicsCaptureSession> g_captureSession;
static wil::com_ptr<IDirect3D11CaptureFramePool> g_framePool;


// Helper: release everything cleanly
static void CleanupInternal()
{
    g_captureItem.reset();
}

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

    // TODO: Fix warning
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
    // Create the capture item for the monitor
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

static HRESULT InitializeD3DDevice()
{
    D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_0 };
    wil::com_ptr<ID3D11Device> device;
    wil::com_ptr<ID3D11DeviceContext> context;

    HRESULT hr = D3D11CreateDevice(
        nullptr,                    // default adapter
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
    if (FAILED(hr)) return hr;

    g_d3dDevice = std::move(device);
    g_d3dContext = std::move(context);
    return S_OK;
}

static bool TryStartCapture(HRESULT* outHr = nullptr)
{
    // Make sure D3D device is ready
    HRESULT hr = InitializeD3DDevice();
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // TODO: create IDirect3DDevice from g_d3dDevice
    // TODO: create frame pool
    // TODO: create capture session
    // TODO: start session
    return true;
}

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

        g_captureItem = GetGraphicsCaptureItemForMonitor(hMonitor, interop, &hr);
        if (!g_captureItem)
        {
            return false;
        }

        bool success = TryStartCapture();
        if (!success)
        {
            return false;
        }

        // TODO: Do something with hr. Maybe return it?

        return true;
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        // Not implemented yet, don't worry about it.
    }

    __declspec(dllexport) void TryStopRecording()
    {
        CleanupInternal();
    }
}