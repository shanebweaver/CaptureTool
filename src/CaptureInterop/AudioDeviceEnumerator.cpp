#include "pch.h"
#include "AudioDeviceEnumerator.h"

AudioDeviceEnumerator::AudioDeviceEnumerator()
{
    // Initialize COM (may already be initialized, that's okay)
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        return;
    }

    // Create device enumerator
    CoCreateInstance(
        __uuidof(MMDeviceEnumerator),
        nullptr,
        CLSCTX_ALL,
        __uuidof(IMMDeviceEnumerator),
        m_enumerator.put_void()
    );
}

AudioDeviceEnumerator::~AudioDeviceEnumerator()
{
}

bool AudioDeviceEnumerator::EnumerateCaptureDevices(std::vector<AudioDeviceInfo>& devices)
{
    return EnumerateDevices(eCapture, devices);
}

bool AudioDeviceEnumerator::EnumerateRenderDevices(std::vector<AudioDeviceInfo>& devices)
{
    return EnumerateDevices(eRender, devices);
}

bool AudioDeviceEnumerator::GetDefaultCaptureDevice(AudioDeviceInfo& deviceInfo)
{
    if (!m_enumerator)
    {
        return false;
    }

    wil::com_ptr<IMMDevice> device;
    HRESULT hr = m_enumerator->GetDefaultAudioEndpoint(eCapture, eConsole, &device);
    if (FAILED(hr) || !device)
    {
        return false;
    }

    return GetDeviceInfo(device.get(), false, deviceInfo);
}

bool AudioDeviceEnumerator::GetDefaultRenderDevice(AudioDeviceInfo& deviceInfo)
{
    if (!m_enumerator)
    {
        return false;
    }

    wil::com_ptr<IMMDevice> device;
    HRESULT hr = m_enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device);
    if (FAILED(hr) || !device)
    {
        return false;
    }

    return GetDeviceInfo(device.get(), true, deviceInfo);
}

bool AudioDeviceEnumerator::EnumerateDevices(EDataFlow dataFlow, std::vector<AudioDeviceInfo>& devices)
{
    if (!m_enumerator)
    {
        return false;
    }

    devices.clear();

    // Get default device for marking
    wil::com_ptr<IMMDevice> defaultDevice;
    m_enumerator->GetDefaultAudioEndpoint(dataFlow, eConsole, &defaultDevice);

    LPWSTR defaultDeviceId = nullptr;
    if (defaultDevice)
    {
        defaultDevice->GetId(&defaultDeviceId);
    }

    // Enumerate all devices
    wil::com_ptr<IMMDeviceCollection> collection;
    HRESULT hr = m_enumerator->EnumAudioEndpoints(dataFlow, DEVICE_STATE_ACTIVE, &collection);
    if (FAILED(hr))
    {
        if (defaultDeviceId)
        {
            CoTaskMemFree(defaultDeviceId);
        }
        return false;
    }

    UINT count;
    collection->GetCount(&count);

    bool isLoopback = (dataFlow == eRender);

    for (UINT i = 0; i < count; i++)
    {
        wil::com_ptr<IMMDevice> device;
        collection->Item(i, &device);

        AudioDeviceInfo info;
        if (GetDeviceInfo(device.get(), isLoopback, info))
        {
            // Check if default
            if (defaultDeviceId && info.deviceId == defaultDeviceId)
            {
                info.isDefault = true;
            }

            devices.push_back(info);
        }
    }

    if (defaultDeviceId)
    {
        CoTaskMemFree(defaultDeviceId);
    }

    return true;
}

bool AudioDeviceEnumerator::GetDeviceInfo(IMMDevice* device, bool isLoopback, AudioDeviceInfo& info)
{
    if (!device)
    {
        return false;
    }

    // Get device ID
    LPWSTR deviceId;
    HRESULT hr = device->GetId(&deviceId);
    if (FAILED(hr))
    {
        return false;
    }

    info.deviceId = deviceId;
    CoTaskMemFree(deviceId);

    // Get property store
    wil::com_ptr<IPropertyStore> props;
    hr = device->OpenPropertyStore(STGM_READ, &props);
    if (FAILED(hr))
    {
        return false;
    }

    // Get friendly name
    PROPVARIANT varName;
    PropVariantInit(&varName);
    hr = props->GetValue(PKEY_Device_FriendlyName, &varName);
    if (SUCCEEDED(hr) && varName.vt == VT_LPWSTR)
    {
        info.friendlyName = varName.pwszVal;
    }
    PropVariantClear(&varName);

    // Get description
    PROPVARIANT varDesc;
    PropVariantInit(&varDesc);
    hr = props->GetValue(PKEY_Device_DeviceDesc, &varDesc);
    if (SUCCEEDED(hr) && varDesc.vt == VT_LPWSTR)
    {
        info.description = varDesc.pwszVal;
    }
    PropVariantClear(&varDesc);

    // Set isLoopback flag
    info.isLoopback = isLoopback;
    info.isDefault = false;  // Will be set by caller

    return true;
}
