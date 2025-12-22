#pragma once
#include "IVideoCaptureSource.h"
#include <memory>

// Forward declarations
struct CaptureSessionConfig;
class IMediaClockReader;

/// <summary>
/// Factory interface for creating video capture sources.
/// Allows different implementations to provide specific video capture mechanisms.
/// </summary>
class IVideoCaptureSourceFactory
{
public:
    virtual ~IVideoCaptureSourceFactory() = default;

    /// <summary>
    /// Create a video capture source configured for the capture session.
    /// </summary>
    /// <param name="config">Configuration for the video capture.</param>
    /// <param name="clockReader">The media clock reader for timestamp synchronization.</param>
    /// <returns>Unique pointer to the created video capture source.</returns>
    virtual std::unique_ptr<IVideoCaptureSource> CreateVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader) = 0;
};
