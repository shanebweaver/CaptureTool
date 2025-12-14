#include "pch.h"
#include "AudioCaptureManager.h"

AudioCaptureManager::AudioCaptureManager() = default;

AudioCaptureManager::~AudioCaptureManager()
{
    Stop();
    if (m_audioFormat)
    {
        CoTaskMemFree(m_audioFormat);
        m_audioFormat = nullptr;
    }
}

HRESULT AudioCaptureManager::Initialize(std::function<void(BYTE*, UINT32, LONGLONG)> onAudioSample)
{
    m_onAudioSample = onAudioSample;

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        return hr;
    }

    // Get the default audio loopback device (render endpoint)
    wil::com_ptr<IMMDeviceEnumerator> enumerator;
    hr = CoCreateInstance(
        __uuidof(MMDeviceEnumerator),
        nullptr,
        CLSCTX_ALL,
        IID_PPV_ARGS(enumerator.put())
    );
    if (FAILED(hr)) return hr;

    hr = enumerator->GetDefaultAudioEndpoint(
        eRender,
        eConsole,
        m_device.put()
    );
    if (FAILED(hr)) return hr;

    // Initialize audio client for loopback capture
    hr = m_device->Activate(
        __uuidof(IAudioClient),
        CLSCTX_ALL,
        nullptr,
        m_audioClient.put_void()
    );
    if (FAILED(hr)) return hr;

    // Get the audio format
    WAVEFORMATEX* mixFormat = nullptr;
    hr = m_audioClient->GetMixFormat(&mixFormat);
    if (FAILED(hr)) return hr;

    // Store a copy of the format
    size_t formatSize = sizeof(WAVEFORMATEX) + mixFormat->cbSize;
    m_audioFormat = (WAVEFORMATEX*)CoTaskMemAlloc(formatSize);
    if (!m_audioFormat)
    {
        CoTaskMemFree(mixFormat);
        return E_OUTOFMEMORY;
    }
    memcpy(m_audioFormat, mixFormat, formatSize);

    // Initialize the audio client in loopback mode
    const REFERENCE_TIME requestedDuration = 10000000; // 1 second in 100ns units
    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        AUDCLNT_STREAMFLAGS_LOOPBACK,
        requestedDuration,
        0,
        mixFormat,
        nullptr
    );

    CoTaskMemFree(mixFormat);
    if (FAILED(hr)) return hr;

    // Get the capture client
    hr = m_audioClient->GetService(IID_PPV_ARGS(m_captureClient.put()));
    if (FAILED(hr)) return hr;

    // Create stop event
    m_stopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    if (!m_stopEvent) return E_FAIL;

    return S_OK;
}

HRESULT AudioCaptureManager::Start()
{
    if (m_isCapturing) return S_OK;

    m_isCapturing = true;
    m_firstAudioTimestamp = 0;

    // Get the current QPC time to sync with video
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_audioStartTime = qpc.QuadPart;

    HRESULT hr = m_audioClient->Start();
    if (FAILED(hr))
    {
        m_isCapturing = false;
        return hr;
    }

    // Start capture thread
    ResetEvent(m_stopEvent);
    m_captureThread = CreateThread(
        nullptr,
        0,
        AudioCaptureThread,
        this,
        0,
        nullptr
    );

    if (!m_captureThread)
    {
        m_audioClient->Stop();
        m_isCapturing = false;
        return E_FAIL;
    }

    return S_OK;
}

void AudioCaptureManager::Stop()
{
    if (!m_isCapturing) return;

    m_isCapturing = false;

    // Signal stop event and wait for thread
    if (m_stopEvent)
    {
        SetEvent(m_stopEvent);
    }

    if (m_captureThread)
    {
        WaitForSingleObject(m_captureThread, 5000);
        CloseHandle(m_captureThread);
        m_captureThread = nullptr;
    }

    if (m_audioClient)
    {
        m_audioClient->Stop();
    }

    if (m_stopEvent)
    {
        CloseHandle(m_stopEvent);
        m_stopEvent = nullptr;
    }
}

DWORD WINAPI AudioCaptureManager::AudioCaptureThread(LPVOID param)
{
    AudioCaptureManager* manager = static_cast<AudioCaptureManager*>(param);
    manager->CaptureLoop();
    return 0;
}

void AudioCaptureManager::CaptureLoop()
{
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        return;
    }

    // Get buffer size for sleep calculation
    UINT32 bufferFrameCount = 0;
    m_audioClient->GetBufferSize(&bufferFrameCount);

    // Calculate sleep time (half the buffer duration)
    REFERENCE_TIME duration = 0;
    m_audioClient->GetDevicePeriod(nullptr, &duration);
    DWORD sleepTime = (DWORD)(duration / 10000 / 2); // Convert to milliseconds

    while (m_isCapturing)
    {
        // Wait for stop event or timeout
        DWORD result = WaitForSingleObject(m_stopEvent, sleepTime);
        if (result == WAIT_OBJECT_0)
        {
            break; // Stop event signaled
        }

        // Get available audio data
        UINT32 packetLength = 0;
        hr = m_captureClient->GetNextPacketSize(&packetLength);
        if (FAILED(hr)) continue;

        while (packetLength > 0)
        {
            BYTE* data = nullptr;
            UINT32 numFramesAvailable = 0;
            DWORD flags = 0;
            UINT64 devicePosition = 0;
            UINT64 qpcPosition = 0;

            hr = m_captureClient->GetBuffer(
                &data,
                &numFramesAvailable,
                &flags,
                &devicePosition,
                &qpcPosition
            );

            if (FAILED(hr)) break;

            // Calculate relative timestamp
            LONGLONG relativeTimestamp = 0;
            if (m_firstAudioTimestamp == 0)
            {
                m_firstAudioTimestamp = qpcPosition;
            }
            
            // Convert QPC to 100ns units relative to start
            LARGE_INTEGER qpcFreq;
            QueryPerformanceFrequency(&qpcFreq);
            LONGLONG qpcDelta = qpcPosition - m_firstAudioTimestamp;
            relativeTimestamp = (qpcDelta * 10000000LL) / qpcFreq.QuadPart;

            // Handle silence flag
            if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0)
            {
                // Fill with silence if needed
                data = nullptr;
            }

            // Call the callback with audio data
            if (m_onAudioSample && numFramesAvailable > 0)
            {
                UINT32 dataSize = numFramesAvailable * m_audioFormat->nBlockAlign;
                m_onAudioSample(data, dataSize, relativeTimestamp);
            }

            m_captureClient->ReleaseBuffer(numFramesAvailable);

            hr = m_captureClient->GetNextPacketSize(&packetLength);
            if (FAILED(hr)) break;
        }
    }

    CoUninitialize();
}
