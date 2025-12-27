#pragma once
#include "Result.h"
#include <Windows.h>
#include <vector>
#include <cstdint>

/// <summary>
/// Represents a screenshot captured from a single monitor.
/// Contains pixel data in BGRA format and monitor metadata.
/// </summary>
struct MonitorScreenshot
{
    HMONITOR hMonitor;
    int width;
    int height;
    int left;
    int top;
    uint32_t dpiX;
    uint32_t dpiY;
    bool isPrimary;
    std::vector<uint8_t> pixelData;  // BGRA format, size = width * height * 4
    
    MonitorScreenshot() 
        : hMonitor(nullptr), width(0), height(0), left(0), top(0),
          dpiX(96), dpiY(96), isPrimary(false) {}
};

/// <summary>
/// Represents a combined screenshot from multiple monitors.
/// Contains pixel data covering the union of all monitor bounds.
/// </summary>
struct CombinedScreenshot
{
    int width;
    int height;
    int left;
    int top;
    std::vector<uint8_t> pixelData;  // BGRA format
    
    CombinedScreenshot() 
        : width(0), height(0), left(0), top(0) {}
};

/// <summary>
/// Interface for capturing screenshots from Windows monitors.
/// Provides operations for single monitor, multi-monitor, and combined capture.
/// All pixel data is in BGRA format for compatibility with Windows GDI.
/// </summary>
class IScreenshotCapture
{
public:
    virtual ~IScreenshotCapture() = default;
    
    /// <summary>
    /// Capture a screenshot from a specific monitor.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <returns>Result containing MonitorScreenshot on success, or error info on failure.</returns>
    virtual Result<MonitorScreenshot> CaptureMonitor(HMONITOR hMonitor) = 0;
    
    /// <summary>
    /// Capture screenshots from all available monitors.
    /// </summary>
    /// <returns>Result containing vector of MonitorScreenshot on success, or error info on failure.</returns>
    virtual Result<std::vector<MonitorScreenshot>> CaptureAllMonitors() = 0;
    
    /// <summary>
    /// Combine multiple monitor screenshots into a single image.
    /// Calculates the union bounds and positions each monitor correctly.
    /// </summary>
    /// <param name="monitors">Vector of monitor screenshots to combine.</param>
    /// <returns>Result containing CombinedScreenshot on success, or error info on failure.</returns>
    virtual Result<CombinedScreenshot> CombineMonitors(const std::vector<MonitorScreenshot>& monitors) = 0;
    
    /// <summary>
    /// Save pixel data to a PNG file using Windows Imaging Component (WIC).
    /// </summary>
    /// <param name="pixelData">Pointer to BGRA pixel data.</param>
    /// <param name="width">Width of the image in pixels.</param>
    /// <param name="height">Height of the image in pixels.</param>
    /// <param name="filePath">Path where PNG file should be saved.</param>
    /// <returns>Result indicating success or error info on failure.</returns>
    virtual Result<void> SaveToPng(const uint8_t* pixelData, int width, int height, const wchar_t* filePath) = 0;
};
