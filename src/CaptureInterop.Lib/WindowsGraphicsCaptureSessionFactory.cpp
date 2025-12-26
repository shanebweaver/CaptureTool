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
    // Validate configuration first (Guard pattern)
    if (!config.IsValid())
    {
        return nullptr;
    }

    // Create the media clock first
    auto mediaClock = m_mediaClockFactory->CreateClock();
    if (!mediaClock)
    {
        return nullptr;
    }

    // Create audio capture source with clock reader
    auto audioCaptureSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(mediaClock.get());
    if (!audioCaptureSource)
    {
        return nullptr;
    }

    // Create video capture source with clock reader
    auto videoCaptureSource = m_videoCaptureSourceFactory->CreateVideoCaptureSource(config, mediaClock.get());
    if (!videoCaptureSource)
    {
        return nullptr;
    }

    // Create sink writer
    auto sinkWriter = m_mp4SinkWriterFactory->CreateSinkWriter();
    if (!sinkWriter)
    {
        return nullptr;
    }

    // Create session with all dependencies - ownership is transferred to the session
    auto session = std::make_unique<WindowsGraphicsCaptureSession>(
        config,
        std::move(mediaClock),
        std::move(audioCaptureSource),
        std::move(videoCaptureSource),
        std::move(sinkWriter));

    // Initialize the session - this sets up all sources and sink writer
    // If initialization fails, return nullptr (fail-fast)
    if (!session->Initialize())
    {
        return nullptr;
    }

    return session;
}
