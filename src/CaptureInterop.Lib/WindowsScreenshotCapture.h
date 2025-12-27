#pragma once
#include "IScreenshotCapture.h"
#include <Windows.h>
#include <wil/com.h>
#include <memory>

/// <summary>
/// RAII wrapper for HDC (Device Context) handles.
/// Automatically calls ReleaseDC or DeleteDC on destruction.
/// </summary>
class GdiDcHandle
{
public:
    /// <summary>
    /// Create from GetDC/GetWindowDC (requires ReleaseDC).
    /// </summary>
    static GdiDcHandle FromGetDC(HWND hwnd, HDC hdc)
    {
        return GdiDcHandle(hwnd, hdc, false);
    }
    
    /// <summary>
    /// Create from CreateCompatibleDC (requires DeleteDC).
    /// </summary>
    static GdiDcHandle FromCreateDC(HDC hdc)
    {
        return GdiDcHandle(nullptr, hdc, true);
    }
    
    ~GdiDcHandle()
    {
        Reset();
    }
    
    // Move semantics
    GdiDcHandle(GdiDcHandle&& other) noexcept
        : m_hwnd(other.m_hwnd), m_hdc(other.m_hdc), m_needsDeleteDC(other.m_needsDeleteDC)
    {
        other.m_hdc = nullptr;
    }
    
    GdiDcHandle& operator=(GdiDcHandle&& other) noexcept
    {
        if (this != &other)
        {
            Reset();
            m_hwnd = other.m_hwnd;
            m_hdc = other.m_hdc;
            m_needsDeleteDC = other.m_needsDeleteDC;
            other.m_hdc = nullptr;
        }
        return *this;
    }
    
    // Delete copy semantics
    GdiDcHandle(const GdiDcHandle&) = delete;
    GdiDcHandle& operator=(const GdiDcHandle&) = delete;
    
    HDC Get() const { return m_hdc; }
    bool IsValid() const { return m_hdc != nullptr; }
    
    void Reset()
    {
        if (m_hdc)
        {
            if (m_needsDeleteDC)
            {
                DeleteDC(m_hdc);
            }
            else
            {
                ReleaseDC(m_hwnd, m_hdc);
            }
            m_hdc = nullptr;
        }
    }
    
private:
    GdiDcHandle(HWND hwnd, HDC hdc, bool needsDeleteDC)
        : m_hwnd(hwnd), m_hdc(hdc), m_needsDeleteDC(needsDeleteDC) {}
    
    HWND m_hwnd;
    HDC m_hdc;
    bool m_needsDeleteDC;
};

/// <summary>
/// RAII wrapper for HBITMAP handles.
/// Automatically calls DeleteObject on destruction.
/// </summary>
class GdiBitmapHandle
{
public:
    explicit GdiBitmapHandle(HBITMAP hBitmap = nullptr)
        : m_hBitmap(hBitmap) {}
    
    ~GdiBitmapHandle()
    {
        Reset();
    }
    
    // Move semantics
    GdiBitmapHandle(GdiBitmapHandle&& other) noexcept
        : m_hBitmap(other.m_hBitmap)
    {
        other.m_hBitmap = nullptr;
    }
    
    GdiBitmapHandle& operator=(GdiBitmapHandle&& other) noexcept
    {
        if (this != &other)
        {
            Reset();
            m_hBitmap = other.m_hBitmap;
            other.m_hBitmap = nullptr;
        }
        return *this;
    }
    
    // Delete copy semantics
    GdiBitmapHandle(const GdiBitmapHandle&) = delete;
    GdiBitmapHandle& operator=(const GdiBitmapHandle&) = delete;
    
    HBITMAP Get() const { return m_hBitmap; }
    bool IsValid() const { return m_hBitmap != nullptr; }
    
    void Reset()
    {
        if (m_hBitmap)
        {
            DeleteObject(m_hBitmap);
            m_hBitmap = nullptr;
        }
    }
    
    HBITMAP* AddressOf() { return &m_hBitmap; }
    
private:
    HBITMAP m_hBitmap;
};

/// <summary>
/// RAII wrapper for SelectObject.
/// Automatically restores the old object on destruction.
/// </summary>
class GdiObjectSelector
{
public:
    GdiObjectSelector(HDC hdc, HGDIOBJ hNewObj)
        : m_hdc(hdc), m_hOldObj(nullptr)
    {
        if (hdc && hNewObj)
        {
            m_hOldObj = SelectObject(hdc, hNewObj);
        }
    }
    
    ~GdiObjectSelector()
    {
        Reset();
    }
    
    // Move semantics
    GdiObjectSelector(GdiObjectSelector&& other) noexcept
        : m_hdc(other.m_hdc), m_hOldObj(other.m_hOldObj)
    {
        other.m_hdc = nullptr;
        other.m_hOldObj = nullptr;
    }
    
    GdiObjectSelector& operator=(GdiObjectSelector&& other) noexcept
    {
        if (this != &other)
        {
            Reset();
            m_hdc = other.m_hdc;
            m_hOldObj = other.m_hOldObj;
            other.m_hdc = nullptr;
            other.m_hOldObj = nullptr;
        }
        return *this;
    }
    
    // Delete copy semantics
    GdiObjectSelector(const GdiObjectSelector&) = delete;
    GdiObjectSelector& operator=(const GdiObjectSelector&) = delete;
    
    void Reset()
    {
        if (m_hdc && m_hOldObj)
        {
            SelectObject(m_hdc, m_hOldObj);
            m_hOldObj = nullptr;
        }
    }
    
private:
    HDC m_hdc;
    HGDIOBJ m_hOldObj;
};

/// <summary>
/// Windows implementation of IScreenshotCapture using GDI for capture.
/// Uses RAII wrappers to ensure proper cleanup of all GDI resources.
/// </summary>
class WindowsScreenshotCapture : public IScreenshotCapture
{
public:
    WindowsScreenshotCapture() = default;
    ~WindowsScreenshotCapture() override = default;
    
    Result<MonitorScreenshot> CaptureMonitor(HMONITOR hMonitor) override;
    Result<std::vector<MonitorScreenshot>> CaptureAllMonitors() override;
    Result<CombinedScreenshot> CombineMonitors(const std::vector<MonitorScreenshot>& monitors) override;
    Result<void> SaveToPng(const uint8_t* pixelData, int width, int height, const wchar_t* filePath) override;
    
private:
    /// <summary>
    /// Extract pixel data from a bitmap in BGRA format.
    /// </summary>
    Result<std::vector<uint8_t>> GetBitmapPixels(HDC hdc, HBITMAP hBitmap, int width, int height);
    
    /// <summary>
    /// Callback function for EnumDisplayMonitors.
    /// </summary>
    static BOOL CALLBACK EnumMonitorCallback(HMONITOR hMonitor, HDC hdcMonitor, LPRECT lprcMonitor, LPARAM lParam);
    
    /// <summary>
    /// Context data passed to EnumMonitorCallback.
    /// </summary>
    struct EnumContext
    {
        WindowsScreenshotCapture* pThis;
        std::vector<MonitorScreenshot>* pResults;
        ErrorInfo* pError;
    };
};
