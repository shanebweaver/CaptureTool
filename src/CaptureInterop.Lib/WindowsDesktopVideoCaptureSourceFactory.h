#pragma once
#include "IVideoCaptureSourceFactory.h"
#include "IMonitorHdrDetectorFactory.h"

/// <summary>
/// Factory for creating Windows System-based video capture sources.
/// </summary>
class WindowsDesktopVideoCaptureSourceFactory : public IVideoCaptureSourceFactory
{
public:
    WindowsDesktopVideoCaptureSourceFactory();
    explicit WindowsDesktopVideoCaptureSourceFactory(std::unique_ptr<IMonitorHdrDetectorFactory> monitorHdrDetectorFactory);
    ~WindowsDesktopVideoCaptureSourceFactory() override = default;

    // IVideoCaptureSourceFactory implementation
    std::unique_ptr<IVideoCaptureSource> CreateVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader) override;

private:
    std::unique_ptr<IMonitorHdrDetectorFactory> m_monitorHdrDetectorFactory;
};
