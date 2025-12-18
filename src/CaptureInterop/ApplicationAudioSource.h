#pragma once
#include "IAudioSource.h"
#include "WindowsVersionHelper.h"
#include <audioclient.h>
#include <audiopolicy.h>
#include <mmdeviceapi.h>
#include <thread>
#include <atomic>
#include <vector>
#include <string>

/// <summary>
/// Audio source for per-application audio capture (Windows 11 22H2+).
/// Uses Audio Session API to isolate specific application's audio.
/// NOTE: This is a simplified implementation. Full per-process isolation
/// requires IAudioClient3 with process loopback mode, which is only available
/// on Windows 11 22H2+. For now, this captures all loopback audio but provides
/// the framework for future per-app capture.
/// </summary>
class ApplicationAudioSource : public IAudioSource
{
public:
    ApplicationAudioSource();
    ~ApplicationAudioSource();

    /// <summary>
    /// Set the process ID to capture audio from.
    /// Must be called before Initialize().
    /// NOTE: Currently not fully implemented - captures all loopback audio.
    /// Full per-process audio requires Windows 11 22H2+ Audio Session API.
    /// </summary>
    void SetProcessId(DWORD processId);
    
    /// <summary>
    /// Get the current process ID.
    /// </summary>
    DWORD GetProcessId() const;
    
    /// <summary>
    /// Check if per-application capture is supported on this system.
    /// Returns true on Windows 11 22H2+.
    /// </summary>
    static bool IsSupported();

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
    DWORD m_processId = 0;
    
    // WASAPI components (currently using standard loopback)
    wil::com_ptr<IMMDeviceEnumerator> m_deviceEnumerator;
    wil::com_ptr<IMMDevice> m_device;
    wil::com_ptr<IAudioClient> m_audioClient;
    wil::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_format = nullptr;
    
    // Capture thread
    AudioSampleCallback m_callback;
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

    // Thread procedures
    void CaptureThreadProc();
    
    // Cleanup
    void Cleanup();
};
