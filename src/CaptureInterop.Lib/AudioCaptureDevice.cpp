#include "pch.h"
#include "AudioCaptureDevice.h"

#include <string>
#include <string_view>

namespace
{
    std::wstring GetWasapiEndpointId(const wchar_t* deviceId)
    {
        if (!deviceId || deviceId[0] == L'\0')
        {
            return L"";
        }

        std::wstring id(deviceId);

        constexpr std::wstring_view mmDeviceApiMarker = L"MMDEVAPI#";
        size_t endpointStart = id.find(mmDeviceApiMarker);
        if (endpointStart == std::wstring::npos)
        {
            return id;
        }

        endpointStart += mmDeviceApiMarker.length();
        size_t endpointEnd = id.find(L'#', endpointStart);
        return id.substr(endpointStart, endpointEnd == std::wstring::npos ? std::wstring::npos : endpointEnd - endpointStart);
    }
}

// ============================================================================
// Constructor / Destructor
// ============================================================================

AudioCaptureDevice::AudioCaptureDevice() = default;

AudioCaptureDevice::~AudioCaptureDevice()
{
    Stop();
    ReleaseResources();
    // Principle #5 (RAII Everything): All COM objects automatically released via wil::com_ptr
    // - m_captureClient, m_audioClient, m_device, m_deviceEnumerator: wil::com_ptr handles Release()
    // - m_waveFormat: wil::unique_cotaskmem_ptr calls CoTaskMemFree()
    // No manual Release() or free() calls needed.
}

// ============================================================================
// Device Initialization
// ============================================================================

bool AudioCaptureDevice::Initialize(bool loopback, const wchar_t* deviceId, HRESULT* outHr)
{
    ReleaseResources();

    // Initialize COM for this thread
    HRESULT hr = S_OK;
    if (!m_comInitialized)
    {
        hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    }
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        if (outHr) *outHr = hr;
        return false;
    }
    if (SUCCEEDED(hr) && !m_comInitialized)
    {
        m_comInitialized = true;
    }

    // Create device enumerator
    hr = CoCreateInstance(
        __uuidof(MMDeviceEnumerator),
        nullptr,
        CLSCTX_ALL,
        __uuidof(IMMDeviceEnumerator),
        m_deviceEnumerator.put_void()
    );
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get selected audio endpoint or fall back to the default endpoint.
    std::wstring endpointId = GetWasapiEndpointId(deviceId);
    if (!endpointId.empty())
    {
        hr = m_deviceEnumerator->GetDevice(endpointId.c_str(), m_device.put());
    }
    else
    {
        EDataFlow dataFlow = loopback ? eRender : eCapture;
        hr = m_deviceEnumerator->GetDefaultAudioEndpoint(
            dataFlow,
            eConsole,
            m_device.put()
        );
    }
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Activate audio client
    hr = m_device->Activate(
        __uuidof(IAudioClient),
        CLSCTX_ALL,
        nullptr,
        m_audioClient.put_void()
    );
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get the audio format
    WAVEFORMATEX* pFormat = nullptr;
    hr = m_audioClient->GetMixFormat(&pFormat);
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    m_waveFormat.reset(pFormat);

    // Initialize audio client for loopback or capture
    DWORD streamFlags = loopback ? AUDCLNT_STREAMFLAGS_LOOPBACK : 0;
    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        streamFlags,
        10000000, // 1 second buffer
        0,
        m_waveFormat.get(),
        nullptr
    );
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get the capture client
    hr = m_audioClient->GetService(
        __uuidof(IAudioCaptureClient),
        m_captureClient.put_void()
    );
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    if (outHr) *outHr = S_OK;
    return true;
}

// ============================================================================
// Capture Control
// ============================================================================

bool AudioCaptureDevice::Start(HRESULT* outHr)
{
    if (!m_audioClient)
    {
        if (outHr) *outHr = E_NOT_VALID_STATE;
        return false;
    }

    HRESULT hr = m_audioClient->Start();
    if (FAILED(hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    m_isCapturing = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void AudioCaptureDevice::Stop()
{
    if (m_audioClient && m_isCapturing)
    {
        m_audioClient->Stop();
        m_isCapturing = false;
    }
}

void AudioCaptureDevice::ReleaseResources()
{
    m_captureClient.reset();
    m_audioClient.reset();
    m_device.reset();
    m_deviceEnumerator.reset();
    m_waveFormat.reset();

    if (m_comInitialized)
    {
        CoUninitialize();
        m_comInitialized = false;
    }
}

// ============================================================================
// Audio Sample Reading
// ============================================================================

UINT32 AudioCaptureDevice::ReadSamples(
    BYTE** ppData,
    UINT32* pNumFramesAvailable,
    DWORD* pFlags,
    UINT64* pDevicePosition,
    UINT64* pQpcPosition)
{
    if (!m_captureClient || !m_isCapturing)
    {
        return 0;
    }

    HRESULT hr = m_captureClient->GetBuffer(
        ppData,
        pNumFramesAvailable,
        pFlags,
        pDevicePosition,
        pQpcPosition
    );

    if (FAILED(hr))
    {
        return 0;
    }

    return *pNumFramesAvailable;
}

void AudioCaptureDevice::ReleaseBuffer(UINT32 numFramesRead)
{
    if (m_captureClient)
    {
        m_captureClient->ReleaseBuffer(numFramesRead);
    }
}
