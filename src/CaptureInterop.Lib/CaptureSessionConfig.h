#pragma once
#include "pch.h"
#include <Windows.h>
#include <cstdint>
#include "CallbackTypes.h"

/// <summary>
/// Configuration settings for a capture session.
/// Contains all parameters needed to initialize and configure screen recording.
/// </summary>
struct CaptureSessionConfig
{
    /// <summary>
    /// Handle to the monitor to capture.
    /// </summary>
    HMONITOR hMonitor;

    /// <summary>
    /// Path to the output MP4 file.
    /// </summary>
    const wchar_t* outputPath;

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
    /// Optional callback for video frames. If set, video frames will be forwarded to managed layer.
    /// </summary>
    VideoFrameCallback videoFrameCallback;

    /// <summary>
    /// Optional callback for audio samples. If set, audio samples will be forwarded to managed layer.
    /// </summary>
    AudioSampleCallback audioSampleCallback;

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
        , outputPath(path)
        , audioEnabled(audio)
        , frameRate(fps)
        , videoBitrate(vidBitrate)
        , audioBitrate(audBitrate)
        , videoFrameCallback(nullptr)
        , audioSampleCallback(nullptr)
    {
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    CaptureSessionConfig()
        : hMonitor(nullptr)
        , outputPath(nullptr)
        , audioEnabled(false)
        , frameRate(30)
        , videoBitrate(5000000)
        , audioBitrate(128000)
        , videoFrameCallback(nullptr)
        , audioSampleCallback(nullptr)
    {
    }
};
