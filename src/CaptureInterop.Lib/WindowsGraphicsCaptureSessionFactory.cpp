#include "pch.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "WindowsGraphicsCaptureSession.h"

std::unique_ptr<ICaptureSession> WindowsGraphicsCaptureSessionFactory::CreateSession(const CaptureSessionConfig& config)
{
    // Create session and configure it
    // For now, we just create the session. The config will be used when Start() is called.
    return std::make_unique<WindowsGraphicsCaptureSession>();
}

std::unique_ptr<ICaptureSession> WindowsGraphicsCaptureSessionFactory::CreateSession()
{
    return std::make_unique<WindowsGraphicsCaptureSession>();
}
