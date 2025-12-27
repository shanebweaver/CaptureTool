#include "ScreenshotExports.h"
#include "WindowsScreenshotCapture.h"
#include <memory>

// Constants
constexpr uint32_t DEFAULT_DPI = 96;

// Opaque handle implementation
struct ScreenshotHandle
{
    MonitorScreenshot monitorData;
    CombinedScreenshot combinedData;
    bool isCombined;
    
    ScreenshotHandle() : isCombined(false) {}
};

// Global screenshot capture instance
// NOTE: This instance is not thread-safe. The exported functions should not be called
// concurrently from multiple threads without external synchronization. The WindowsScreenshotCapture
// class uses local variables and temporary GDI objects which are thread-safe per call, but
// concurrent calls could lead to race conditions in error handling and COM initialization.
static WindowsScreenshotCapture g_screenshotCapture;

extern "C"
{
    __declspec(dllexport) ScreenshotHandle* CaptureMonitorScreenshot(HMONITOR hMonitor)
    {
        auto result = g_screenshotCapture.CaptureMonitor(hMonitor);
        if (result.IsError())
        {
            return nullptr;
        }
        
        auto handle = new (std::nothrow) ScreenshotHandle();
        if (!handle)
        {
            return nullptr;
        }
        
        handle->monitorData = std::move(result.Value());
        handle->isCombined = false;
        return handle;
    }
    
    __declspec(dllexport) ScreenshotHandle* CaptureAllMonitorsScreenshot()
    {
        auto monitorsResult = g_screenshotCapture.CaptureAllMonitors();
        if (monitorsResult.IsError())
        {
            return nullptr;
        }
        
        auto combinedResult = g_screenshotCapture.CombineMonitors(monitorsResult.Value());
        if (combinedResult.IsError())
        {
            return nullptr;
        }
        
        auto handle = new (std::nothrow) ScreenshotHandle();
        if (!handle)
        {
            return nullptr;
        }
        
        handle->combinedData = std::move(combinedResult.Value());
        handle->isCombined = true;
        return handle;
    }
    
    __declspec(dllexport) void GetScreenshotInfo(
        ScreenshotHandle* handle,
        int* width,
        int* height,
        int* left,
        int* top,
        uint32_t* dpiX,
        uint32_t* dpiY,
        bool* isPrimary)
    {
        if (!handle)
        {
            return;
        }
        
        if (handle->isCombined)
        {
            if (width) *width = handle->combinedData.width;
            if (height) *height = handle->combinedData.height;
            if (left) *left = handle->combinedData.left;
            if (top) *top = handle->combinedData.top;
            if (dpiX) *dpiX = DEFAULT_DPI;
            if (dpiY) *dpiY = DEFAULT_DPI;
            if (isPrimary) *isPrimary = false;
        }
        else
        {
            if (width) *width = handle->monitorData.width;
            if (height) *height = handle->monitorData.height;
            if (left) *left = handle->monitorData.left;
            if (top) *top = handle->monitorData.top;
            if (dpiX) *dpiX = handle->monitorData.dpiX;
            if (dpiY) *dpiY = handle->monitorData.dpiY;
            if (isPrimary) *isPrimary = handle->monitorData.isPrimary;
        }
    }
    
    __declspec(dllexport) bool CopyScreenshotPixels(
        ScreenshotHandle* handle,
        uint8_t* buffer,
        int bufferSize)
    {
        if (!handle || !buffer || bufferSize <= 0)
        {
            return false;
        }
        
        const std::vector<uint8_t>* pixelData = nullptr;
        
        if (handle->isCombined)
        {
            pixelData = &handle->combinedData.pixelData;
        }
        else
        {
            pixelData = &handle->monitorData.pixelData;
        }
        
        if (static_cast<size_t>(bufferSize) < pixelData->size())
        {
            return false;
        }
        
        std::memcpy(buffer, pixelData->data(), pixelData->size());
        return true;
    }
    
    __declspec(dllexport) bool SaveScreenshotToPng(
        ScreenshotHandle* handle,
        const wchar_t* filePath)
    {
        if (!handle || !filePath)
        {
            return false;
        }
        
        const uint8_t* pixelData = nullptr;
        int width = 0;
        int height = 0;
        
        if (handle->isCombined)
        {
            pixelData = handle->combinedData.pixelData.data();
            width = handle->combinedData.width;
            height = handle->combinedData.height;
        }
        else
        {
            pixelData = handle->monitorData.pixelData.data();
            width = handle->monitorData.width;
            height = handle->monitorData.height;
        }
        
        auto result = g_screenshotCapture.SaveToPng(pixelData, width, height, filePath);
        return result.IsOk();
    }
    
    __declspec(dllexport) void FreeScreenshot(ScreenshotHandle* handle)
    {
        delete handle;
    }
    
    __declspec(dllexport) ScreenshotHandle* CombineScreenshots(
        ScreenshotHandle** handles,
        int count)
    {
        if (!handles || count <= 0)
        {
            return nullptr;
        }
        
        std::vector<MonitorScreenshot> monitors;
        monitors.reserve(count);
        
        for (int i = 0; i < count; i++)
        {
            if (!handles[i] || handles[i]->isCombined)
            {
                // Can only combine monitor screenshots, not already-combined ones
                return nullptr;
            }
            // Use move semantics to avoid copying large pixel buffers
            monitors.push_back(std::move(handles[i]->monitorData));
        }
        
        auto result = g_screenshotCapture.CombineMonitors(monitors);
        if (result.IsError())
        {
            return nullptr;
        }
        
        auto handle = new (std::nothrow) ScreenshotHandle();
        if (!handle)
        {
            return nullptr;
        }
        
        handle->combinedData = std::move(result.Value());
        handle->isCombined = true;
        return handle;
    }
}
