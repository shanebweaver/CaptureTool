#pragma once
#include "pch.h"
#include "IAudioCaptureSourceFactory.h"

/// <summary>
/// Factory implementation for creating WindowsSystemAudioCaptureSource instances.
/// Creates audio capture sources using Windows WASAPI loopback mode for system audio.
/// </summary>
class WindowsSystemAudioCaptureSourceFactory : public IAudioCaptureSourceFactory
{
public:
    WindowsSystemAudioCaptureSourceFactory() = default;
    ~WindowsSystemAudioCaptureSourceFactory() override = default;

    // Delete copy and move operations
    WindowsSystemAudioCaptureSourceFactory(const WindowsSystemAudioCaptureSourceFactory&) = delete;
    WindowsSystemAudioCaptureSourceFactory& operator=(const WindowsSystemAudioCaptureSourceFactory&) = delete;
    WindowsSystemAudioCaptureSourceFactory(WindowsSystemAudioCaptureSourceFactory&&) = delete;
    WindowsSystemAudioCaptureSourceFactory& operator=(WindowsSystemAudioCaptureSourceFactory&&) = delete;

    /// <summary>
    /// Create a new WindowsSystemAudioCaptureSource instance.
    /// </summary>
    /// <returns>A unique pointer to a new WindowsSystemAudioCaptureSource.</returns>
    std::unique_ptr<IAudioCaptureSource> CreateAudioCaptureSource() override;
};
