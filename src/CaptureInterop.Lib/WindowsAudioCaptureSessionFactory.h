#pragma once
#include "AudioRecordingConfig.h"
#include "IAudioCaptureSourceFactory.h"
#include "IMediaClockFactory.h"
#include "IWavSinkWriterFactory.h"
#include "WindowsAudioCaptureSession.h"
#include <memory>

class WindowsAudioCaptureSessionFactory
{
public:
    WindowsAudioCaptureSessionFactory(
        std::unique_ptr<IMediaClockFactory> mediaClockFactory,
        std::unique_ptr<IAudioCaptureSourceFactory> audioCaptureSourceFactory,
        std::unique_ptr<IWavSinkWriterFactory> wavSinkWriterFactory);

    std::unique_ptr<WindowsAudioCaptureSession> CreateSession(const AudioRecordingConfig& config);

private:
    std::unique_ptr<IMediaClockFactory> m_mediaClockFactory;
    std::unique_ptr<IAudioCaptureSourceFactory> m_audioCaptureSourceFactory;
    std::unique_ptr<IWavSinkWriterFactory> m_wavSinkWriterFactory;
};
