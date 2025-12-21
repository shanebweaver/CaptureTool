#pragma once
#include "IAudioCaptureSource.h"
#include <memory>

/// <summary>
/// Factory interface for creating audio capture source instances.
/// Provides abstraction for audio source creation to enable dependency injection and testing.
/// </summary>
class IAudioCaptureSourceFactory
{
public:
    virtual ~IAudioCaptureSourceFactory() = default;

    /// <summary>
    /// Create a new audio capture source instance.
    /// </summary>
    /// <returns>A unique pointer to a new IAudioCaptureSource implementation.</returns>
    virtual std::unique_ptr<IAudioCaptureSource> CreateAudioCaptureSource() = 0;
};
