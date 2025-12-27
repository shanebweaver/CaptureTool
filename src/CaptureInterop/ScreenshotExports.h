#pragma once
#include <Windows.h>
#include <cstdint>

// Opaque handle for screenshot data
struct ScreenshotHandle;

extern "C"
{
    /// <summary>
    /// Capture screenshot from a specific monitor.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <returns>Handle to screenshot data, or nullptr on failure. Caller must free with FreeScreenshot.</returns>
    __declspec(dllexport) ScreenshotHandle* CaptureMonitorScreenshot(HMONITOR hMonitor);
    
    /// <summary>
    /// Capture screenshots from all available monitors.
    /// Returns a handle representing all monitors combined.
    /// </summary>
    /// <returns>Handle to combined screenshot data, or nullptr on failure. Caller must free with FreeScreenshot.</returns>
    __declspec(dllexport) ScreenshotHandle* CaptureAllMonitorsScreenshot();
    
    /// <summary>
    /// Get information about a captured screenshot.
    /// </summary>
    /// <param name="handle">Screenshot handle obtained from capture function.</param>
    /// <param name="width">Receives width in pixels.</param>
    /// <param name="height">Receives height in pixels.</param>
    /// <param name="left">Receives left coordinate.</param>
    /// <param name="top">Receives top coordinate.</param>
    /// <param name="dpiX">Receives horizontal DPI.</param>
    /// <param name="dpiY">Receives vertical DPI.</param>
    /// <param name="isPrimary">Receives whether this is the primary monitor.</param>
    __declspec(dllexport) void GetScreenshotInfo(
        ScreenshotHandle* handle,
        int* width,
        int* height,
        int* left,
        int* top,
        uint32_t* dpiX,
        uint32_t* dpiY,
        bool* isPrimary);
    
    /// <summary>
    /// Copy screenshot pixel data to a managed buffer.
    /// </summary>
    /// <param name="handle">Screenshot handle.</param>
    /// <param name="buffer">Destination buffer (must be large enough).</param>
    /// <param name="bufferSize">Size of destination buffer in bytes.</param>
    /// <returns>True if copy succeeded, false otherwise.</returns>
    __declspec(dllexport) bool CopyScreenshotPixels(
        ScreenshotHandle* handle,
        uint8_t* buffer,
        int bufferSize);
    
    /// <summary>
    /// Save screenshot to PNG file.
    /// </summary>
    /// <param name="handle">Screenshot handle.</param>
    /// <param name="filePath">Path where PNG should be saved.</param>
    /// <returns>True if save succeeded, false otherwise.</returns>
    __declspec(dllexport) bool SaveScreenshotToPng(
        ScreenshotHandle* handle,
        const wchar_t* filePath);
    
    /// <summary>
    /// Free screenshot handle and associated memory.
    /// </summary>
    /// <param name="handle">Screenshot handle to free.</param>
    __declspec(dllexport) void FreeScreenshot(ScreenshotHandle* handle);
    
    /// <summary>
    /// Combine multiple screenshot handles into a single combined screenshot.
    /// </summary>
    /// <param name="handles">Array of screenshot handles to combine.</param>
    /// <param name="count">Number of handles in the array.</param>
    /// <returns>Handle to combined screenshot, or nullptr on failure. Caller must free with FreeScreenshot.</returns>
    __declspec(dllexport) ScreenshotHandle* CombineScreenshots(
        ScreenshotHandle** handles,
        int count);
}
