#include "pch.h"
#include "ScreenRecorder.h"
#include <windows.graphics.capture.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <wrl.h>
#include <wrl/wrappers/corewrappers.h>
#include <d3d11.h>
#include <roapi.h>
#include <mfapi.h>
#include <thread>
#include <atomic>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace ABI::Windows::Graphics::Capture;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;

static ComPtr<ID3D11Device> g_d3d11Device;
static ComPtr<IDirect3DDevice> g_d3dDevice;
static ComPtr<IGraphicsCaptureItem> g_captureItem;
static std::thread g_captureThread;
static std::atomic<bool> g_recording = false;

// Simple dummy capture loop
void CaptureLoop()
{
    while (g_recording)
    {
        // Here you would grab frames from the GraphicsCaptureItem
        // For now just sleep to simulate work
        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }
}

extern "C"
{
    bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath)
    {
        HRESULT hr = RoInitialize(RO_INIT_MULTITHREADED);
        if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
            return false;

        // 1️⃣ Create D3D11 device
        D3D_FEATURE_LEVEL featureLevel;
        hr = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nullptr, 0,
            D3D11_SDK_VERSION,
            &g_d3d11Device,
            &featureLevel,
            nullptr);
        if (FAILED(hr))
            return false;

        ComPtr<IDXGIDevice> dxgiDevice;
        hr = g_d3d11Device.As(&dxgiDevice); // QI for IDXGIDevice
        if (FAILED(hr))
            return false;

        // 2️⃣ Wrap D3D device for WinRT
        hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.Get(), &g_d3dDevice);
        if (FAILED(hr))
            return false;

        // 3️⃣ Create GraphicsCaptureItem for monitor
        ComPtr<IGraphicsCaptureItemInterop> interop;
        ComPtr<IActivationFactory> factory;
        HStringReference className(RuntimeClass_Windows_Graphics_Capture_GraphicsCaptureItem);
        hr = RoGetActivationFactory(className.Get(), IID_PPV_ARGS(&factory));
        if (FAILED(hr))
            return false;

        hr = factory.As(&interop);
        if (FAILED(hr))
            return false;

        ComPtr<IGraphicsCaptureItem> item;
        hr = interop->CreateForMonitor(
            hMonitor,
            __uuidof(IGraphicsCaptureItem),
            reinterpret_cast<void**>(item.GetAddressOf()));
        if (FAILED(hr))
            return false;

        g_captureItem = item;

        // 4️⃣ Start dummy capture thread
        g_recording = true;
        g_captureThread = std::thread(CaptureLoop);

        return true;
    }

    void TryPauseRecording()
    {
        // Implement pause if needed
    }

    void TryStopRecording()
    {
        g_recording = false;
        if (g_captureThread.joinable())
            g_captureThread.join();

        g_captureItem.Reset();
        g_d3dDevice.Reset();
        g_d3d11Device.Reset();
    }
}
