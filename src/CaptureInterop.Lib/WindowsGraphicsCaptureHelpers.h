#pragma once
#include "pch.h"

using namespace ABI::Windows::Graphics;
using namespace ABI::Windows::Graphics::Capture;
using namespace ABI::Windows::Graphics::DirectX;
using namespace ABI::Windows::Graphics::DirectX::Direct3D11;

namespace WindowsGraphicsCaptureHelpers
{
    /// <summary>
    /// Helper structure containing D3D11 device and context.
    /// </summary>
    struct D3DDeviceAndContext
    {
        wil::com_ptr<ID3D11Device> device;
        wil::com_ptr<ID3D11DeviceContext> context;
    };

    /// <summary>
    /// Get the Graphics Capture Item interop factory.
    /// Required for creating capture items from HWNDs or HMONITORs.
    /// </summary>
    inline wil::com_ptr<IGraphicsCaptureItemInterop> GetGraphicsCaptureItemInterop(HRESULT* outHr = nullptr)
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

    /// <summary>
    /// Create a Graphics Capture Item for a specific monitor.
    /// </summary>
    inline wil::com_ptr<IGraphicsCaptureItem> GetGraphicsCaptureItemForMonitor(
        HMONITOR hMonitor, 
        wil::com_ptr<IGraphicsCaptureItemInterop> interop, 
        HRESULT* outHr = nullptr)
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

    /// <summary>
    /// Initialize Direct3D 11 device and context with BGRA support for capture.
    /// </summary>
    inline D3DDeviceAndContext InitializeD3D(HRESULT* outHr = nullptr)
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

        if (outHr) *outHr = S_OK;
        return { std::move(device), std::move(context) };
    }

    /// <summary>
    /// Create a WinRT Direct3DDevice from a D3D11 device.
    /// Required for interop with Windows.Graphics.Capture API.
    /// </summary>
    inline wil::com_ptr<IDirect3DDevice> CreateDirect3DDevice(
        wil::com_ptr<ID3D11Device> device, 
        HRESULT* outHr = nullptr)
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

    /// <summary>
    /// Create a Direct3D11 capture frame pool for buffering captured frames.
    /// </summary>
    inline wil::com_ptr<IDirect3D11CaptureFramePool> CreateCaptureFramePool(
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
            6, // number of buffers - increased from 2 to prevent stalls when WriteSample blocks
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

    /// <summary>
    /// Create a Graphics Capture Session from a frame pool and capture item.
    /// </summary>
    inline wil::com_ptr<IGraphicsCaptureSession> CreateCaptureSession(
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

        if (outHr) *outHr = S_OK;
        return session;
    }
}
