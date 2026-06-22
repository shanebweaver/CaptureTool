#pragma once
#include "AudioRecordingConfig.h"
#include "CallbackHandle.h"
#include "CallbackRegistry.h"
#include "CallbackTypes.h"
#include "CaptureSessionState.h"
#include "IAudioCaptureSource.h"
#include "IMediaClock.h"
#include "IWavSinkWriter.h"
#include <atomic>
#include <memory>

class WindowsAudioCaptureSession
{
public:
    WindowsAudioCaptureSession(
        const AudioRecordingConfig& config,
        std::unique_ptr<IMediaClock> mediaClock,
        std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
        std::unique_ptr<IWavSinkWriter> sinkWriter);
    ~WindowsAudioCaptureSession();

    WindowsAudioCaptureSession(const WindowsAudioCaptureSession&) = delete;
    WindowsAudioCaptureSession& operator=(const WindowsAudioCaptureSession&) = delete;

    bool Initialize(HRESULT* outHr = nullptr);
    bool Start(HRESULT* outHr = nullptr);
    void Stop();
    void Pause();
    void Resume();
    void ToggleAudioCapture(bool enabled);
    bool SetAudioInputSource(const wchar_t* sourceId);
    void SetAudioInputVolume(uint32_t volumePercentage);
    void SetAudioSampleCallback(AudioSampleCallback callback);

private:
    void SetupAudioCallback();

    AudioRecordingConfig m_config;
    std::unique_ptr<IMediaClock> m_mediaClock;
    std::unique_ptr<IAudioCaptureSource> m_audioCaptureSource;
    std::unique_ptr<IWavSinkWriter> m_sinkWriter;
    CaptureSessionStateMachine m_stateMachine;
    CaptureInterop::CallbackRegistry<AudioSampleData> m_audioCallbackRegistry;
    CaptureInterop::CallbackHandle m_audioCallbackHandle;
    std::atomic<bool> m_isShuttingDown{ false };
    bool m_audioAvailable = false;
    bool m_cleanupCompleted = false;
};
