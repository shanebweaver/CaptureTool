#include "pch.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "WindowsGraphicsCaptureSession.h"

WindowsGraphicsCaptureSessionFactory::WindowsGraphicsCaptureSessionFactory(
    std::unique_ptr<IMediaClockFactory> mediaClockFactory,
    std::unique_ptr<IAudioCaptureSourceFactory> audioCaptureSourceFactory,
    std::unique_ptr<IVideoCaptureSourceFactory> videoCaptureSourceFactory,
    std::unique_ptr<IMP4SinkWriterFactory> mp4SinkWriterFactory)
    : m_mediaClockFactory(std::move(mediaClockFactory))
    , m_audioCaptureSourceFactory(std::move(audioCaptureSourceFactory))
    , m_videoCaptureSourceFactory(std::move(videoCaptureSourceFactory))
    , m_mp4SinkWriterFactory(std::move(mp4SinkWriterFactory))
{
}

std::unique_ptr<ICaptureSession> WindowsGraphicsCaptureSessionFactory::CreateSession(const CaptureSessionConfig& config)
{
    // Create session and configure it
    // For now, we just create the session. The config will be used when Start() is called.
    return std::make_unique<WindowsGraphicsCaptureSession>(
        config,
        m_mediaClockFactory.get(),
        m_audioCaptureSourceFactory.get(),
        m_videoCaptureSourceFactory.get(),
        m_mp4SinkWriterFactory.get());
}
