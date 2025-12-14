#pragma once

class AudioCaptureManager
{
public:
    AudioCaptureManager();
    ~AudioCaptureManager();

    // Initialize audio capture with callback for audio samples
    HRESULT Initialize(std::function<void(BYTE*, UINT32, LONGLONG)> onAudioSample);

    // Start capturing audio from the default loopback device
    HRESULT Start();

    // Stop capturing audio
    void Stop();

    // Get audio format information
    WAVEFORMATEX* GetAudioFormat() const { return m_audioFormat; }

    bool IsCapturing() const { return m_isCapturing; }

private:
    // Audio capture thread
    static DWORD WINAPI AudioCaptureThread(LPVOID param);
    void CaptureLoop();

    wil::com_ptr<IMMDevice> m_device;
    wil::com_ptr<IAudioClient> m_audioClient;
    wil::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_audioFormat = nullptr;
    
    HANDLE m_captureThread = nullptr;
    HANDLE m_stopEvent = nullptr;
    HANDLE m_audioReadyEvent = nullptr;
    HANDLE m_initCompleteEvent = nullptr;
    volatile bool m_isCapturing = false;
    
    std::function<void(BYTE*, UINT32, LONGLONG)> m_onAudioSample;
    LONGLONG m_firstAudioTimestamp = 0;
    REFERENCE_TIME m_audioStartTime = 0;
};
