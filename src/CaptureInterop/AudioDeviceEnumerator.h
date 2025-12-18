#pragma once
#include <vector>
#include <string>
#include <mmdeviceapi.h>
#include <functiondiscoverykeys_devpkey.h>

/// <summary>
/// Information about an audio device.
/// </summary>
struct AudioDeviceInfo
{
    std::wstring deviceId;       // Unique device identifier
    std::wstring friendlyName;   // Human-readable name
    std::wstring description;    // Device description
    bool isDefault;              // True if this is the default device
    bool isLoopback;             // True for render devices (desktop audio), false for capture
};

/// <summary>
/// Enumerates audio devices using WASAPI.
/// </summary>
class AudioDeviceEnumerator
{
public:
    AudioDeviceEnumerator();
    ~AudioDeviceEnumerator();

    /// <summary>
    /// Enumerate all audio capture devices (microphones).
    /// </summary>
    /// <param name="devices">Output vector to receive device information.</param>
    /// <returns>True if enumeration succeeded.</returns>
    bool EnumerateCaptureDevices(std::vector<AudioDeviceInfo>& devices);
    
    /// <summary>
    /// Enumerate all audio render devices (speakers, for loopback).
    /// </summary>
    /// <param name="devices">Output vector to receive device information.</param>
    /// <returns>True if enumeration succeeded.</returns>
    bool EnumerateRenderDevices(std::vector<AudioDeviceInfo>& devices);
    
    /// <summary>
    /// Get the default capture device.
    /// </summary>
    /// <param name="deviceInfo">Output parameter to receive device information.</param>
    /// <returns>True if successful.</returns>
    bool GetDefaultCaptureDevice(AudioDeviceInfo& deviceInfo);
    
    /// <summary>
    /// Get the default render device.
    /// </summary>
    /// <param name="deviceInfo">Output parameter to receive device information.</param>
    /// <returns>True if successful.</returns>
    bool GetDefaultRenderDevice(AudioDeviceInfo& deviceInfo);

private:
    bool EnumerateDevices(EDataFlow dataFlow, std::vector<AudioDeviceInfo>& devices);
    bool GetDeviceInfo(IMMDevice* device, bool isLoopback, AudioDeviceInfo& info);
    
    wil::com_ptr<IMMDeviceEnumerator> m_enumerator;
};
