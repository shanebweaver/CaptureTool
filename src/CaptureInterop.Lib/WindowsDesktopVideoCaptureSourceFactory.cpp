#include "pch.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "WindowsMonitorHdrDetectorFactory.h"

WindowsDesktopVideoCaptureSourceFactory::WindowsDesktopVideoCaptureSourceFactory()
    : WindowsDesktopVideoCaptureSourceFactory(std::make_unique<WindowsMonitorHdrDetectorFactory>())
{
}

WindowsDesktopVideoCaptureSourceFactory::WindowsDesktopVideoCaptureSourceFactory(
    std::unique_ptr<IMonitorHdrDetectorFactory> monitorHdrDetectorFactory)
    : m_monitorHdrDetectorFactory(std::move(monitorHdrDetectorFactory))
{
}

std::unique_ptr<IVideoCaptureSource> WindowsDesktopVideoCaptureSourceFactory::CreateVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader)
{
    std::unique_ptr<IMonitorHdrDetector> monitorHdrDetector;
    if (m_monitorHdrDetectorFactory)
    {
        monitorHdrDetector = m_monitorHdrDetectorFactory->CreateMonitorHdrDetector();
    }

    return std::make_unique<WindowsDesktopVideoCaptureSource>(config, clockReader, std::move(monitorHdrDetector));
}
