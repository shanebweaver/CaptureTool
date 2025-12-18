#pragma once
#include <Windows.h>

/// <summary>
/// Helper for detecting Windows version.
/// </summary>
class WindowsVersionHelper
{
public:
    /// <summary>
    /// Check if running on Windows 11 22H2 or later.
    /// Required for per-application audio capture via Audio Session API.
    /// </summary>
    static bool IsWindows11_22H2OrLater();
    
    /// <summary>
    /// Get Windows build number.
    /// </summary>
    static DWORD GetBuildNumber();
};
