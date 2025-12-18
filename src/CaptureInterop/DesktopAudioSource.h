#pragma once
#include "IAudioSource.h"
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>
#include <vector>

/// <summary>
/// Audio source implementation for desktop audio (loopback) capture using WASAPI.
/// Encapsulates all desktop audio capture logic in a reusable, callback-based source.
/// Maintains timestamp accumulation to prevent audio speedup issues.
/// </summary>
class DesktopAudioSource : public IAudioSource
{
public:
    DesktopAudioSource();
    ~DesktopAudioSource();

    // IAudioSource implementation
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioCallback(AudioSampleCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    // Reference counting
    volatile long m_ref = 1;

    // Audio capture
    AudioCaptureDevice m_device;
    AudioSampleCallback m_callback;
    
    // Capture thread
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};
    std::atomic<bool> m_isInitialized{false};
    
    // Synchronization (for integration with MP4SinkWriter)
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;
    
    // Silent buffer management (for muting)
    std::atomic<bool> m_wasDisabled{false};
    std::atomic<int> m_samplesToSkip{0};
    std::vector<BYTE> m_silentBuffer;

    // Thread procedure
    void CaptureThreadProc();
    
    // Cleanup
    void Cleanup();
};
