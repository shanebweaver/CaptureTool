#include "pch.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "WindowsGraphicsCaptureSession.h"

WindowsGraphicsCaptureSessionFactory::WindowsGraphicsCaptureSessionFactory(
    IMediaClockFactory* mediaClockFactory,
    IAudioCaptureSourceFactory* audioCaptureSourceFactory,
    IVideoCaptureSourceFactory* videoCaptureSourceFactory)
    : m_mediaClockFactory(mediaClockFactory)
    , m_audioCaptureSourceFactory(audioCaptureSourceFactory)
    , m_videoCaptureSourceFactory(videoCaptureSourceFactory)
{
}

std::unique_ptr<ICaptureSession> WindowsGraphicsCaptureSessionFactory::CreateSession(const CaptureSessionConfig& config)
{
    // Create session and configure it
    // For now, we just create the session. The config will be used when Start() is called.
    return std::make_unique<WindowsGraphicsCaptureSession>(
        config,
        m_mediaClockFactory,
        m_audioCaptureSourceFactory,
        m_videoCaptureSourceFactory);
}
