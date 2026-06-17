#pragma once
#include "pch.h"
#include <Windows.h>
#include <cstdint>
#include <string>
#include <vector>

enum class CaptureTargetType : uint32_t
{
    Monitor = 0,
    Window = 1,
    Rectangle = 2
};

/// <summary>
/// Result of configuration validation with detailed error information.
/// Provides comprehensive feedback about validation failures.
/// </summary>
struct ConfigValidationResult
{
    bool isValid;
    std::vector<std::string> errors;
    std::vector<std::string> warnings;
    
    /// <summary>
    /// Create a successful validation result.
    /// </summary>
    static ConfigValidationResult Ok()
    {
        return ConfigValidationResult{true, {}, {}};
    }
    
    /// <summary>
    /// Add an error to the validation result.
    /// Marks the result as invalid.
    /// </summary>
    void AddError(const std::string& error)
    {
        errors.push_back(error);
        isValid = false;
    }
    
    /// <summary>
    /// Add a warning to the validation result.
    /// Does not affect validity.
    /// </summary>
    void AddWarning(const std::string& warning)
    {
        warnings.push_back(warning);
    }
};

/// <summary>
/// Configuration settings for a capture session.
/// Contains all parameters needed to initialize and configure screen recording.
/// 
/// The configuration owns all its data (including the output path string),
/// ensuring clear lifetime semantics and safe copying/moving.
/// 
/// Supports C++20 designated initializers for cleaner initialization:
/// <code>
/// CaptureSessionConfig config
/// {
///     .hMonitor = hMonitor,
///     .outputPath = L"output.mp4",
///     .audioEnabled = true,
///     .frameRate = 30,
///     .videoBitrate = 5'000'000,
///     .audioBitrate = 128'000
/// };
/// </code>
/// </summary>
struct CaptureSessionConfig
{
    CaptureTargetType targetType;

    /// <summary>
    /// Handle to the monitor to capture.
    /// </summary>
    HMONITOR hMonitor;

    HWND hwnd;
    int32_t sourceLeft;
    int32_t sourceTop;
    uint32_t sourceWidth;
    uint32_t sourceHeight;

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
    /// Optional Windows audio input endpoint id. Empty uses the existing default audio source.
    /// </summary>
    std::wstring audioInputSourceId;

    /// <summary>
    /// Microphone/input volume as a percentage. 100 preserves the captured signal.
    /// </summary>
    uint32_t audioInputVolumePercentage;

    /// <summary>
    /// Target video frame rate (FPS). Default is 30.
    /// </summary>
    uint32_t frameRate;

    /// <summary>
    /// Target video bitrate in bits per second. Default is 5'000'000 (5 Mbps).
    /// </summary>
    uint32_t videoBitrate;

    /// <summary>
    /// Target audio bitrate in bits per second. Default is 128'000 (128 kbps).
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
        uint32_t vidBitrate = 5'000'000,
        uint32_t audBitrate = 128'000,
        std::wstring audioSourceId = L"",
        uint32_t audioVolumePercentage = 100)
        : targetType(CaptureTargetType::Monitor)
        , hMonitor(monitor)
        , hwnd(nullptr)
        , sourceLeft(0)
        , sourceTop(0)
        , sourceWidth(0)
        , sourceHeight(0)
        , outputPath(path ? path : L"")
        , audioEnabled(audio)
        , frameRate(fps)
        , videoBitrate(vidBitrate)
        , audioBitrate(audBitrate)
        , audioInputSourceId(std::move(audioSourceId))
        , audioInputVolumePercentage(audioVolumePercentage)
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
        uint32_t vidBitrate = 5'000'000,
        uint32_t audBitrate = 128'000,
        std::wstring audioSourceId = L"",
        uint32_t audioVolumePercentage = 100)
        : targetType(CaptureTargetType::Monitor)
        , hMonitor(monitor)
        , hwnd(nullptr)
        , sourceLeft(0)
        , sourceTop(0)
        , sourceWidth(0)
        , sourceHeight(0)
        , outputPath(std::move(path))
        , audioEnabled(audio)
        , frameRate(fps)
        , videoBitrate(vidBitrate)
        , audioBitrate(audBitrate)
        , audioInputSourceId(std::move(audioSourceId))
        , audioInputVolumePercentage(audioVolumePercentage)
    {
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    CaptureSessionConfig()
        : targetType(CaptureTargetType::Monitor)
        , hMonitor(nullptr)
        , hwnd(nullptr)
        , sourceLeft(0)
        , sourceTop(0)
        , sourceWidth(0)
        , sourceHeight(0)
        , outputPath(L"")
        , audioEnabled(false)
        , frameRate(30)
        , videoBitrate(5'000'000)
        , audioBitrate(128'000)
        , audioInputSourceId(L"")
        , audioInputVolumePercentage(100)
    {
    }

    static CaptureSessionConfig ForMonitor(
        HMONITOR monitor,
        std::wstring path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5'000'000,
        uint32_t audBitrate = 128'000,
        std::wstring audioSourceId = L"",
        uint32_t audioVolumePercentage = 100)
    {
        CaptureSessionConfig config(monitor, std::move(path), audio, fps, vidBitrate, audBitrate, std::move(audioSourceId), audioVolumePercentage);
        config.targetType = CaptureTargetType::Monitor;
        return config;
    }

    static CaptureSessionConfig ForWindow(
        HWND window,
        std::wstring path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5'000'000,
        uint32_t audBitrate = 128'000,
        std::wstring audioSourceId = L"",
        uint32_t audioVolumePercentage = 100)
    {
        CaptureSessionConfig config(nullptr, std::move(path), audio, fps, vidBitrate, audBitrate, std::move(audioSourceId), audioVolumePercentage);
        config.targetType = CaptureTargetType::Window;
        config.hwnd = window;
        return config;
    }

    static CaptureSessionConfig ForRectangle(
        HMONITOR monitor,
        int32_t left,
        int32_t top,
        uint32_t width,
        uint32_t height,
        std::wstring path,
        bool audio = false,
        uint32_t fps = 30,
        uint32_t vidBitrate = 5'000'000,
        uint32_t audBitrate = 128'000,
        std::wstring audioSourceId = L"",
        uint32_t audioVolumePercentage = 100)
    {
        CaptureSessionConfig config(monitor, std::move(path), audio, fps, vidBitrate, audBitrate, std::move(audioSourceId), audioVolumePercentage);
        config.targetType = CaptureTargetType::Rectangle;
        config.sourceLeft = left;
        config.sourceTop = top;
        config.sourceWidth = width;
        config.sourceHeight = height;
        return config;
    }

    /// <summary>
    /// Validate that the configuration has valid values.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    bool IsValid() const
    {
        return Validate().isValid;
    }
    
    /// <summary>
    /// Comprehensive validation with detailed feedback.
    /// Validates all configuration parameters and provides specific error messages.
    /// </summary>
    /// <returns>Validation result with errors and warnings.</returns>
    ConfigValidationResult Validate() const
    {
        ConfigValidationResult result = ConfigValidationResult::Ok();
        
        if (targetType == CaptureTargetType::Monitor && hMonitor == nullptr)
        {
            result.AddError("hMonitor is required");
        }
        else if (targetType == CaptureTargetType::Window && hwnd == nullptr)
        {
            result.AddError("hwnd is required");
        }
        else if (targetType == CaptureTargetType::Rectangle)
        {
            if (hMonitor == nullptr)
            {
                result.AddError("hMonitor is required");
            }

            if (sourceWidth == 0 || sourceHeight == 0)
            {
                result.AddError("rectangle width and height are required");
            }
        }
            
        if (outputPath.empty())
        {
            result.AddError("outputPath is required");
        }
        else if (!IsValidOutputPath(outputPath))
        {
            result.AddError("outputPath is not writable or parent directory doesn't exist");
        }
            
        // Frame rate validation
        if (frameRate < 1 || frameRate > 120)
        {
            result.AddError("frameRate must be between 1 and 120 (got " + 
                          std::to_string(frameRate) + ")");
        }
        else if (frameRate < 15)
        {
            result.AddWarning("frameRate is very low (" + 
                            std::to_string(frameRate) + "), video may appear choppy");
        }
                            
        // Video bitrate validation
        constexpr uint32_t MIN_VIDEO_BITRATE = 100'000;    // 100 kbps
        constexpr uint32_t MAX_VIDEO_BITRATE = 50'000'000; // 50 Mbps
        
        if (videoBitrate < MIN_VIDEO_BITRATE || videoBitrate > MAX_VIDEO_BITRATE)
        {
            result.AddError("videoBitrate must be between " + 
                          std::to_string(MIN_VIDEO_BITRATE) + " and " + 
                          std::to_string(MAX_VIDEO_BITRATE) + " (got " +
                          std::to_string(videoBitrate) + ")");
        }
        else if (videoBitrate < 1'000'000)
        {
            result.AddWarning("videoBitrate is low (" + 
                            std::to_string(videoBitrate) + "), quality may be poor");
        }
                            
        // Audio bitrate validation
        constexpr uint32_t MIN_AUDIO_BITRATE = 32'000;    // 32 kbps
        constexpr uint32_t MAX_AUDIO_BITRATE = 320'000;   // 320 kbps
        
        if (audioEnabled)
        {
            if (audioBitrate < MIN_AUDIO_BITRATE || audioBitrate > MAX_AUDIO_BITRATE)
            {
                result.AddError("audioBitrate must be between " + 
                              std::to_string(MIN_AUDIO_BITRATE) + " and " + 
                              std::to_string(MAX_AUDIO_BITRATE) + " (got " +
                              std::to_string(audioBitrate) + ")");
            }

            if (audioInputVolumePercentage > 100)
            {
                result.AddError("audioInputVolumePercentage must be between 0 and 100 (got " +
                    std::to_string(audioInputVolumePercentage) + ")");
            }
        }
        
        return result;
    }

private:
    /// <summary>
    /// Check if the output path is valid and writable.
    /// </summary>
    static bool IsValidOutputPath(const std::wstring& path)
    {
        // Check if the path has a valid structure
        if (path.length() < 3)  // Minimum: "C:\a.mp4"
        {
            return false;
        }
        
        // Check if path contains invalid characters
        // Note: ':' is only valid at position 1 for drive separator (e.g., "C:")
        const std::wstring invalidChars = L"<>:\"|?*";
        for (wchar_t c : invalidChars)
        {
            size_t pos = path.find(c);
            if (pos != std::wstring::npos)
            {
                // Allow ':' only at position 1 for drive separator
                if (c == L':' && pos == 1)
                {
                    continue;
                }
                // All other occurrences of invalid chars (including ':' elsewhere) are invalid
                return false;
            }
        }
        
        // Check if the file extension is present
        size_t dotPos = path.find_last_of(L'.');
        if (dotPos == std::wstring::npos || dotPos == path.length() - 1)
        {
            return false;
        }
        
        return true;
    }
};
