#pragma once
#include "pch.h"
#include <Windows.h>
#include <cstdint>
#include <string>

/// <summary>
/// Configuration settings for a capture session.
/// Contains all parameters needed to initialize and configure screen recording.
/// 
/// The configuration owns all its data (including the output path string),
/// ensuring clear lifetime semantics and safe copying/moving.
/// </summary>
struct CaptureSessionConfig
{
    /// <summary>
    /// Handle to the monitor to capture.
    /// </summary>
    HMONITOR hMonitor;

    /// <summary>
    /// Path to the output MP4 file.
    /// The config owns this string, ensuring clear lifetime semantics.
    /// </summary>
    std::wstring outputPath;

    /// <summary>
    /// Whether to capture system audio.
    /// </summary>
    bool audioEnabled;

    /// <summary>
    /// Target video frame rate (FPS). Default is 30.
    /// </summary>
    uint32_t frameRate;

    /// <summary>
    /// Target video bitrate in bits per second. Default is 5000000 (5 Mbps).
    /// </summary>
    uint32_t videoBitrate;

    /// <summary>
    /// Target audio bitrate in bits per second. Default is 128000 (128 kbps).
    /// </summary>
    uint32_t audioBitrate;

    /// <summary>
    /// Constructor with required parameters and default values for optional parameters.
    /// </summary>
    CaptureSessionConfig(
        HMONITOR monitor,
        const wchar_t* path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5000000,
        uint32_t audBitrate = 128000)
        : hMonitor(monitor)
        , outputPath(path ? path : L"")
        , audioEnabled(audio)
        , frameRate(fps)
        , videoBitrate(vidBitrate)
        , audioBitrate(audBitrate)
    {
    }

    /// <summary>
    /// Constructor with std::wstring path for better ownership semantics.
    /// </summary>
    CaptureSessionConfig(
        HMONITOR monitor,
        std::wstring path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5000000,
        uint32_t audBitrate = 128000)
        : hMonitor(monitor)
        , outputPath(std::move(path))
        , audioEnabled(audio)
        , frameRate(fps)
        , videoBitrate(vidBitrate)
        , audioBitrate(audBitrate)
    {
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    CaptureSessionConfig()
        : hMonitor(nullptr)
        , outputPath(L"")
        , audioEnabled(false)
        , frameRate(30)
        , videoBitrate(5000000)
        , audioBitrate(128000)
    {
    }

    /// <summary>
    /// Validate that the configuration has valid values.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    bool IsValid() const
    {
        return hMonitor != nullptr && !outputPath.empty();
    }
};
