#include "pch.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSource.h"

std::unique_ptr<IVideoCaptureSource> WindowsDesktopVideoCaptureSourceFactory::CreateVideoCaptureSource(const CaptureSessionConfig& config, IMediaClockReader* clockReader)
{
    return std::make_unique<WindowsDesktopVideoCaptureSource>(config, clockReader);
}
