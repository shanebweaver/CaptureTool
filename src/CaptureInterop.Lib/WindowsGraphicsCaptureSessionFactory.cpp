#include "pch.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "WindowsGraphicsCaptureSession.h"

WindowsGraphicsCaptureSessionFactory::WindowsGraphicsCaptureSessionFactory(
    IMediaClockFactory* mediaClockFactory,
    IAudioCaptureSourceFactory* audioCaptureSourceFactory)
    : m_mediaClockFactory(mediaClockFactory)
    , m_audioCaptureSourceFactory(audioCaptureSourceFactory)
{
}

std::unique_ptr<ICaptureSession> WindowsGraphicsCaptureSessionFactory::CreateSession(const CaptureSessionConfig& config)
{
    // Create session and configure it
    // For now, we just create the session. The config will be used when Start() is called.
    return std::make_unique<WindowsGraphicsCaptureSession>(config, m_mediaClockFactory, m_audioCaptureSourceFactory);
}
