#pragma once
#include "IAudioCaptureSource.h"
#include "AudioCaptureHandler.h"
#include <memory>

/// <summary>
/// Audio input source that captures system audio using WASAPI loopback mode.
/// Implements IAudioCaptureSource to provide system-wide audio capture.
/// </summary>
class WasapiAudioCaptureSource : public IAudioCaptureSource
{
public:
    WasapiAudioCaptureSource();
    ~WasapiAudioCaptureSource() override;

    // IAudioCaptureSource implementation
    bool Initialize(HRESULT* outHr = nullptr) override;
    bool Start(HRESULT* outHr = nullptr) override;
    void Stop() override;
    WAVEFORMATEX* GetFormat() const override;
    void SetSinkWriter(MP4SinkWriter* sinkWriter) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;
    bool IsRunning() const override;

private:
    std::unique_ptr<AudioCaptureHandler> m_handler;
};
