#pragma once
#include "AudioRecordingConfig.h"
#include "CallbackTypes.h"
#include "WindowsAudioCaptureSession.h"
#include "WindowsAudioCaptureSessionFactory.h"
#include <memory>
#include <mutex>

class AudioRecorderImpl
{
public:
    explicit AudioRecorderImpl(std::unique_ptr<WindowsAudioCaptureSessionFactory> factory);
    AudioRecorderImpl();
    ~AudioRecorderImpl();

    bool StartRecording(const AudioRecordingConfig& config, HRESULT* outHr = nullptr);
    bool PauseRecording();
    bool ResumeRecording();
    bool StopRecording();
    bool SetAudioCaptureEnabled(bool enabled);
    bool SetAudioInputSource(const wchar_t* sourceId);
    bool SetAudioInputVolume(uint32_t volumePercentage);
    void SetAudioSampleCallback(AudioSampleCallback callback);

private:
    std::mutex m_mutex;
    std::unique_ptr<WindowsAudioCaptureSession> m_captureSession;
    std::unique_ptr<WindowsAudioCaptureSessionFactory> m_factory;
    AudioSampleCallback m_audioSampleCallback = nullptr;
};
