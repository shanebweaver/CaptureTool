#pragma once
#include "pch.h"
#include "IAudioCaptureSourceFactory.h"

/// <summary>
/// Factory implementation for creating WindowsLocalAudioCaptureSource instances.
/// Creates audio capture sources using Windows WASAPI loopback mode for system audio.
/// </summary>
class WindowsLocalAudioCaptureSourceFactory : public IAudioCaptureSourceFactory
{
public:
    WindowsLocalAudioCaptureSourceFactory() = default;
    ~WindowsLocalAudioCaptureSourceFactory() override = default;

    // Delete copy and move operations
    WindowsLocalAudioCaptureSourceFactory(const WindowsLocalAudioCaptureSourceFactory&) = delete;
    WindowsLocalAudioCaptureSourceFactory& operator=(const WindowsLocalAudioCaptureSourceFactory&) = delete;
    WindowsLocalAudioCaptureSourceFactory(WindowsLocalAudioCaptureSourceFactory&&) = delete;
    WindowsLocalAudioCaptureSourceFactory& operator=(WindowsLocalAudioCaptureSourceFactory&&) = delete;

    /// <summary>
    /// Create a new WindowsLocalAudioCaptureSource instance.
    /// </summary>
    /// <param name="clockReader">The media clock reader for timestamp synchronization.</param>
    /// <returns>A unique pointer to a new WindowsLocalAudioCaptureSource.</returns>
    std::unique_ptr<IAudioCaptureSource> CreateAudioCaptureSource(IMediaClockReader* clockReader) override;
};
