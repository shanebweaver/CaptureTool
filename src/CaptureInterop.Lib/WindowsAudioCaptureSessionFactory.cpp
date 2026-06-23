#include "pch.h"
#include "WindowsAudioCaptureSessionFactory.h"
#include <strsafe.h>

WindowsAudioCaptureSessionFactory::WindowsAudioCaptureSessionFactory(
    std::unique_ptr<IMediaClockFactory> mediaClockFactory,
    std::unique_ptr<IAudioCaptureSourceFactory> audioCaptureSourceFactory,
    std::unique_ptr<IWavSinkWriterFactory> wavSinkWriterFactory)
    : m_mediaClockFactory(std::move(mediaClockFactory))
    , m_audioCaptureSourceFactory(std::move(audioCaptureSourceFactory))
    , m_wavSinkWriterFactory(std::move(wavSinkWriterFactory))
{
}

std::unique_ptr<WindowsAudioCaptureSession> WindowsAudioCaptureSessionFactory::CreateSession(const AudioRecordingConfig& config)
{
    if (!config.IsValid())
    {
        return nullptr;
    }

    auto mediaClock = m_mediaClockFactory->CreateClock();
    if (!mediaClock)
    {
        return nullptr;
    }

    auto audioCaptureSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(mediaClock.get(), config.audioInputSourceId);
    if (!audioCaptureSource)
    {
        return nullptr;
    }

    auto sinkWriter = m_wavSinkWriterFactory->CreateSinkWriter();
    if (!sinkWriter)
    {
        return nullptr;
    }

    auto session = std::make_unique<WindowsAudioCaptureSession>(
        config,
        std::move(mediaClock),
        std::move(audioCaptureSource),
        std::move(sinkWriter));

    HRESULT hr = S_OK;
    if (!session->Initialize(&hr))
    {
        wchar_t message[160]{};
        StringCchPrintfW(
            message,
            ARRAYSIZE(message),
            L"[CaptureInterop Audio] CreateSession initialization failed. HRESULT=0x%08X\r\n",
            static_cast<unsigned int>(hr));
        OutputDebugStringW(message);
        return nullptr;
    }

    return session;
}
