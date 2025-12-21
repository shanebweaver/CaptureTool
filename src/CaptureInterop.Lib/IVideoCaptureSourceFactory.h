#pragma once
#include "IVideoCaptureSource.h"
#include <memory>

// Forward declaration
struct CaptureSessionConfig;

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
    /// <returns>Unique pointer to the created video capture source.</returns>
    virtual std::unique_ptr<IVideoCaptureSource> CreateVideoCaptureSource(const CaptureSessionConfig& config) = 0;
};
