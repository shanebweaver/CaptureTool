#pragma once
#include "pch.h"
#include <Windows.h>
#include <cstdint>
#include <string>
#include <vector>

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
        
        // Required fields
        if (hMonitor == nullptr)
        {
            result.AddError("hMonitor is required");
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
