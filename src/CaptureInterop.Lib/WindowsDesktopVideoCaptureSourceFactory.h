#pragma once
#include "IVideoCaptureSourceFactory.h"

/// <summary>
/// Factory for creating Windows System-based video capture sources.
/// </summary>
class WindowsDesktopVideoCaptureSourceFactory : public IVideoCaptureSourceFactory
{
public:
    WindowsDesktopVideoCaptureSourceFactory() = default;
    ~WindowsDesktopVideoCaptureSourceFactory() override = default;

    // IVideoCaptureSourceFactory implementation
    std::unique_ptr<IVideoCaptureSource> CreateVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader) override;
};
