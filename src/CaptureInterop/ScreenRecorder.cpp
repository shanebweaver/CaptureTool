#include "pch.h"
#include "ScreenRecorder.h"

#include <wil/com.h>

#include <windows.graphics.capture.h>
#include <windows.graphics.capture.interop.h>

#include <roapi.h>

using namespace ABI::Windows::Graphics::Capture;

// Globals (later we can wrap in a struct)
static wil::com_ptr<IGraphicsCaptureItem> g_captureItem;

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
    if (FAILED(hr))
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

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath)
    {
        HRESULT hr = S_OK;

        wil::com_ptr<IGraphicsCaptureItemInterop> interop = GetGraphicsCaptureItemInterop();
        if (!interop)
        {
            return false;
        }

        g_captureItem = GetGraphicsCaptureItemForMonitor(hMonitor, interop, &hr);
        if (!g_captureItem)
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
        CleanupInternal();
    }
}