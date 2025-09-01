#include "pch.h"
#include "ScreenRecorder.h"
#include "MP4SinkWriter.h"
#include "FrameArrivedHandler.h"
#include "GraphicsCaptureHelpers.cpp"

using namespace GraphicsCaptureHelpers;

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