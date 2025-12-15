#pragma once
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>

// Represents an audio capture device (loopback or microphone)
class AudioCaptureDevice
{
public:
    AudioCaptureDevice();
    ~AudioCaptureDevice();

    // Initialize the audio capture device (loopback = true for system audio, false for microphone)
    bool Initialize(bool loopback, HRESULT* outHr = nullptr);
    
    // Start capturing audio
    bool Start(HRESULT* outHr = nullptr);
    
    // Stop capturing audio
    void Stop();
    
    // Get the audio format
    WAVEFORMATEX* GetFormat() const { return m_waveFormat; }
    
    // Read available audio samples
    // Returns number of frames read, or 0 if no data available
    UINT32 ReadSamples(BYTE** ppData, UINT32* pNumFramesAvailable, DWORD* pFlags, UINT64* pDevicePosition, UINT64* pQpcPosition);
    
    // Release the buffer after reading
    void ReleaseBuffer(UINT32 numFramesRead);

private:
    wil::com_ptr<IMMDeviceEnumerator> m_deviceEnumerator;
    wil::com_ptr<IMMDevice> m_device;
    wil::com_ptr<IAudioClient> m_audioClient;
    wil::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_waveFormat = nullptr;
    bool m_isCapturing = false;
};
