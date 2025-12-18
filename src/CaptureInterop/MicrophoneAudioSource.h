#pragma once
#include "IAudioSource.h"
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>
#include <vector>
#include <string>

/// <summary>
/// Audio source for microphone capture using WASAPI capture endpoint.
/// Similar to DesktopAudioSource but captures from microphone instead of loopback.
/// </summary>
class MicrophoneAudioSource : public IAudioSource
{
public:
    MicrophoneAudioSource();
    ~MicrophoneAudioSource();

    /// <summary>
    /// Set the device ID to capture from.
    /// Must be called before Initialize(). If not set, uses default microphone.
    /// </summary>
    /// <param name="deviceId">WASAPI device ID string.</param>
    void SetDeviceId(const std::wstring& deviceId);
    
    /// <summary>
    /// Get the currently selected device ID.
    /// </summary>
    std::wstring GetDeviceId() const;

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

    // Configuration
    std::wstring m_deviceId;  // Empty = default device
    
    // Audio capture
    AudioCaptureDevice m_device;
    AudioSampleCallback m_callback;
    
    // Capture thread
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};
    std::atomic<bool> m_isInitialized{false};
    
    // Synchronization
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;
    
    // Silent buffer management
    std::atomic<bool> m_wasDisabled{false};
    std::atomic<int> m_samplesToSkip{0};
    std::vector<BYTE> m_silentBuffer;

    // Thread procedure
    void CaptureThreadProc();
    
    // Cleanup
    void Cleanup();
};
