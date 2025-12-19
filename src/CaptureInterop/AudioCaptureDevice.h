#pragma once
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>

/// <summary>
/// WASAPI-based audio capture device wrapper.
/// Supports both loopback capture (system audio) and microphone input.
/// </summary>
class AudioCaptureDevice
{
public:
    AudioCaptureDevice();
    ~AudioCaptureDevice();

    /// <summary>
    /// Initialize the audio capture device using WASAPI.
    /// </summary>
    /// <param name="loopback">True for system audio (loopback), false for microphone input.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <param name="deviceId">Optional specific device ID. If nullptr/empty, uses default device.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    bool Initialize(bool loopback, HRESULT* outHr = nullptr, const wchar_t* deviceId = nullptr);
    
    /// <summary>
    /// Start audio capture from the initialized device.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if capture started successfully, false otherwise.</returns>
    bool Start(HRESULT* outHr = nullptr);
    
    /// <summary>
    /// Stop audio capture. Safe to call multiple times.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Get the audio format (sample rate, channels, bit depth) of the capture device.
    /// </summary>
    /// <returns>Pointer to WAVEFORMATEX structure, or nullptr if not initialized.</returns>
    WAVEFORMATEX* GetFormat() const { return m_waveFormat; }
    
    /// <summary>
    /// Read available audio samples from the WASAPI buffer.
    /// Must call ReleaseBuffer() after processing the data.
    /// </summary>
    /// <returns>Number of audio frames read, or 0 if no data available.</returns>
    UINT32 ReadSamples(BYTE** ppData, UINT32* pNumFramesAvailable, DWORD* pFlags, UINT64* pDevicePosition, UINT64* pQpcPosition);
    
    /// <summary>
    /// Release the audio buffer after reading. Must be called after ReadSamples().
    /// </summary>
    /// <param name="numFramesRead">Number of frames that were read from the buffer.</param>
    void ReleaseBuffer(UINT32 numFramesRead);

private:
    wil::com_ptr<IMMDeviceEnumerator> m_deviceEnumerator;
    wil::com_ptr<IMMDevice> m_device;
    wil::com_ptr<IAudioClient> m_audioClient;
    wil::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_waveFormat = nullptr;
    bool m_isCapturing = false;
};
