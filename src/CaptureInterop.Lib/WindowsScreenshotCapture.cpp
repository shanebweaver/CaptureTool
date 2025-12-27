#include "pch.h"
#include "WindowsScreenshotCapture.h"
#include <wincodec.h>
#include <ShellScalingApi.h>
#include <algorithm>

#pragma comment(lib, "Shcore.lib")
#pragma comment(lib, "Windowscodecs.lib")
#pragma comment(lib, "Gdi32.lib")

Result<MonitorScreenshot> WindowsScreenshotCapture::CaptureMonitor(HMONITOR hMonitor)
{
    if (!hMonitor)
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Invalid monitor handle", "CaptureMonitor"));
    }
    
    // Get monitor info
    MONITORINFOEXW monitorInfo = {};
    monitorInfo.cbSize = sizeof(MONITORINFOEXW);
    if (!GetMonitorInfoW(hMonitor, (LPMONITORINFO)&monitorInfo))
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "GetMonitorInfoW"));
    }
    
    int left = monitorInfo.rcMonitor.left;
    int top = monitorInfo.rcMonitor.top;
    int width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
    int height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
    
    // Validate dimensions
    const int MAX_DIMENSION = 32768;
    if (width <= 0 || height <= 0 || width > MAX_DIMENSION || height > MAX_DIMENSION)
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromMessage(E_FAIL, "Invalid monitor dimensions", "CaptureMonitor"));
    }
    
    // Get DPI
    UINT dpiX = 96, dpiY = 96;
    HRESULT hr = GetDpiForMonitor(hMonitor, MDT_DEFAULT, &dpiX, &dpiY);
    if (FAILED(hr))
    {
        // Non-fatal, continue with default DPI
        dpiX = dpiY = 96;
    }
    
    bool isPrimary = (monitorInfo.dwFlags & MONITORINFOF_PRIMARY) != 0;
    
    // Create device contexts
    auto hdcScreen = GdiDcHandle::FromGetDC(nullptr, GetDC(nullptr));
    if (!hdcScreen.IsValid())
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "GetDC"));
    }
    
    auto hdcMem = GdiDcHandle::FromCreateDC(CreateCompatibleDC(hdcScreen.Get()));
    if (!hdcMem.IsValid())
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "CreateCompatibleDC"));
    }
    
    // Create compatible bitmap
    GdiBitmapHandle hBitmap(CreateCompatibleBitmap(hdcScreen.Get(), width, height));
    if (!hBitmap.IsValid())
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "CreateCompatibleBitmap"));
    }
    
    // Select bitmap into memory DC
    GdiObjectSelector selector(hdcMem.Get(), hBitmap.Get());
    
    // Capture screen using BitBlt
    if (!BitBlt(hdcMem.Get(), 0, 0, width, height, hdcScreen.Get(), left, top, SRCCOPY))
    {
        return Result<MonitorScreenshot>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "BitBlt"));
    }
    
    // Extract pixel data
    auto pixelsResult = GetBitmapPixels(hdcMem.Get(), hBitmap.Get(), width, height);
    if (pixelsResult.IsError())
    {
        return Result<MonitorScreenshot>::Error(pixelsResult.Error());
    }
    
    // Build result
    MonitorScreenshot screenshot;
    screenshot.hMonitor = hMonitor;
    screenshot.width = width;
    screenshot.height = height;
    screenshot.left = left;
    screenshot.top = top;
    screenshot.dpiX = dpiX;
    screenshot.dpiY = dpiY;
    screenshot.isPrimary = isPrimary;
    screenshot.pixelData = std::move(pixelsResult.Value());
    
    return Result<MonitorScreenshot>::Ok(std::move(screenshot));
}

Result<std::vector<uint8_t>> WindowsScreenshotCapture::GetBitmapPixels(HDC hdc, HBITMAP hBitmap, int width, int height)
{
    // Check for potential integer overflow in buffer size calculation
    size_t bufferSize = static_cast<size_t>(width) * static_cast<size_t>(height) * 4;
    const size_t MAX_BUFFER_SIZE = 4294967296;  // 4GB limit
    if (bufferSize > MAX_BUFFER_SIZE)
    {
        return Result<std::vector<uint8_t>>::Error(
            ErrorInfo::FromMessage(E_FAIL, "Buffer size calculation overflow", "GetBitmapPixels"));
    }
    
    // Prepare BITMAPINFO for 32bpp top-down DIB
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;  // Negative for top-down
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    bmi.bmiHeader.biSizeImage = static_cast<DWORD>(bufferSize);
    
    std::vector<uint8_t> pixels(bufferSize);
    
    int lines = GetDIBits(hdc, hBitmap, 0, height, pixels.data(), &bmi, DIB_RGB_COLORS);
    if (lines == 0)
    {
        return Result<std::vector<uint8_t>>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "GetDIBits"));
    }
    
    return Result<std::vector<uint8_t>>::Ok(std::move(pixels));
}

BOOL CALLBACK WindowsScreenshotCapture::EnumMonitorCallback(HMONITOR hMonitor, HDC hdcMonitor, LPRECT lprcMonitor, LPARAM lParam)
{
    auto* pContext = reinterpret_cast<EnumContext*>(lParam);
    
    auto result = pContext->pThis->CaptureMonitor(hMonitor);
    if (result.IsError())
    {
        // Store error and stop enumeration
        *pContext->pError = result.Error();
        return FALSE;
    }
    
    pContext->pResults->push_back(std::move(result.Value()));
    return TRUE;
}

Result<std::vector<MonitorScreenshot>> WindowsScreenshotCapture::CaptureAllMonitors()
{
    std::vector<MonitorScreenshot> results;
    ErrorInfo error = ErrorInfo::Success();
    
    EnumContext context;
    context.pThis = this;
    context.pResults = &results;
    context.pError = &error;
    
    if (!EnumDisplayMonitors(nullptr, nullptr, EnumMonitorCallback, reinterpret_cast<LPARAM>(&context)))
    {
        if (!error.IsSuccess())
        {
            return Result<std::vector<MonitorScreenshot>>::Error(error);
        }
        
        return Result<std::vector<MonitorScreenshot>>::Error(
            ErrorInfo::FromHResult(HRESULT_FROM_WIN32(GetLastError()), "EnumDisplayMonitors"));
    }
    
    if (results.empty())
    {
        return Result<std::vector<MonitorScreenshot>>::Error(
            ErrorInfo::FromMessage(E_FAIL, "No monitors found", "CaptureAllMonitors"));
    }
    
    return Result<std::vector<MonitorScreenshot>>::Ok(std::move(results));
}

Result<CombinedScreenshot> WindowsScreenshotCapture::CombineMonitors(const std::vector<MonitorScreenshot>& monitors)
{
    if (monitors.empty())
    {
        return Result<CombinedScreenshot>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "No monitors to combine", "CombineMonitors"));
    }
    
    // Calculate union bounds
    int minX = monitors[0].left;
    int minY = monitors[0].top;
    int maxX = monitors[0].left + monitors[0].width;
    int maxY = monitors[0].top + monitors[0].height;
    
    for (const auto& monitor : monitors)
    {
        minX = std::min(minX, monitor.left);
        minY = std::min(minY, monitor.top);
        maxX = std::max(maxX, monitor.left + monitor.width);
        maxY = std::max(maxY, monitor.top + monitor.height);
    }
    
    int finalWidth = maxX - minX;
    int finalHeight = maxY - minY;
    
    // Sanity checks to prevent integer overflow and excessive memory allocation
    const int MAX_DIMENSION = 32768;  // Reasonable maximum for screen dimensions
    if (finalWidth <= 0 || finalHeight <= 0 ||
        finalWidth > MAX_DIMENSION || finalHeight > MAX_DIMENSION)
    {
        return Result<CombinedScreenshot>::Error(
            ErrorInfo::FromMessage(E_FAIL, "Combined screenshot dimensions are invalid or too large", "CombineMonitors"));
    }
    
    // Check for potential integer overflow in buffer size calculation
    size_t bufferSize = static_cast<size_t>(finalWidth) * static_cast<size_t>(finalHeight) * 4;
    const size_t MAX_BUFFER_SIZE = 4294967296;  // 4GB limit
    if (bufferSize > MAX_BUFFER_SIZE)
    {
        return Result<CombinedScreenshot>::Error(
            ErrorInfo::FromMessage(E_FAIL, "Combined screenshot buffer size exceeds maximum", "CombineMonitors"));
    }
    
    // Create combined pixel buffer (initialize to black)
    std::vector<uint8_t> finalBuffer(bufferSize, 0);
    
    // Copy each monitor's pixels to correct position
    for (const auto& monitor : monitors)
    {
        int offsetX = monitor.left - minX;
        int offsetY = monitor.top - minY;
        
        // Bounds check to prevent buffer overflow
        if (offsetX < 0 || offsetY < 0 || 
            offsetX + monitor.width > finalWidth || 
            offsetY + monitor.height > finalHeight)
        {
            return Result<CombinedScreenshot>::Error(
                ErrorInfo::FromMessage(E_FAIL, "Monitor bounds exceed combined buffer", "CombineMonitors"));
        }
        
        for (int y = 0; y < monitor.height; y++)
        {
            int srcRowStart = y * monitor.width * 4;
            int dstRowStart = ((offsetY + y) * finalWidth + offsetX) * 4;
            
            std::memcpy(
                finalBuffer.data() + dstRowStart,
                monitor.pixelData.data() + srcRowStart,
                monitor.width * 4);
        }
    }
    
    CombinedScreenshot combined;
    combined.width = finalWidth;
    combined.height = finalHeight;
    combined.left = minX;
    combined.top = minY;
    combined.pixelData = std::move(finalBuffer);
    
    return Result<CombinedScreenshot>::Ok(std::move(combined));
}

Result<void> WindowsScreenshotCapture::SaveToPng(const uint8_t* pixelData, int width, int height, const wchar_t* filePath)
{
    if (!pixelData || width <= 0 || height <= 0 || !filePath)
    {
        return Result<void>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Invalid parameters", "SaveToPng"));
    }
    
    // RAII wrapper for COM initialization
    struct ComInitializer
    {
        bool initialized;
        ComInitializer() : initialized(false)
        {
            HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            // Track if we successfully initialized COM (S_OK or S_FALSE means already initialized)
            initialized = SUCCEEDED(hr) && (hr != S_FALSE);
        }
        ~ComInitializer()
        {
            if (initialized)
            {
                CoUninitialize();
            }
        }
    };
    
    ComInitializer comInit;
    
    // Create WIC factory
    wil::com_ptr<IWICImagingFactory> pFactory;
    HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&pFactory));
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "CoCreateInstance(WICImagingFactory)"));
    }
    
    // Create stream
    wil::com_ptr<IWICStream> pStream;
    hr = pFactory->CreateStream(&pStream);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "CreateStream"));
    }
    
    hr = pStream->InitializeFromFilename(filePath, GENERIC_WRITE);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "InitializeFromFilename"));
    }
    
    // Create PNG encoder
    wil::com_ptr<IWICBitmapEncoder> pEncoder;
    hr = pFactory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &pEncoder);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "CreateEncoder"));
    }
    
    hr = pEncoder->Initialize(pStream.get(), WICBitmapEncoderNoCache);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "Initialize encoder"));
    }
    
    // Create frame
    wil::com_ptr<IWICBitmapFrameEncode> pFrame;
    hr = pEncoder->CreateNewFrame(&pFrame, nullptr);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "CreateNewFrame"));
    }
    
    hr = pFrame->Initialize(nullptr);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "Initialize frame"));
    }
    
    hr = pFrame->SetSize(width, height);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "SetSize"));
    }
    
    // Set pixel format to BGRA
    WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat32bppBGRA;
    hr = pFrame->SetPixelFormat(&pixelFormat);
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "SetPixelFormat"));
    }
    
    // Write pixels
    UINT stride = width * 4;
    UINT bufferSize = stride * height;
    hr = pFrame->WritePixels(height, stride, bufferSize, const_cast<BYTE*>(pixelData));
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "WritePixels"));
    }
    
    hr = pFrame->Commit();
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "Commit frame"));
    }
    
    hr = pEncoder->Commit();
    if (FAILED(hr))
    {
        return Result<void>::Error(ErrorInfo::FromHResult(hr, "Commit encoder"));
    }
    
    return Result<void>::Ok();
}
