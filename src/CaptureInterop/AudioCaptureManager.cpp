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

    // Create event for audio buffer notifications
    m_audioReadyEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    if (!m_audioReadyEvent)
    {
        CoTaskMemFree(mixFormat);
        return E_FAIL;
    }

    // Initialize the audio client in loopback mode with event-driven capture
    const REFERENCE_TIME requestedDuration = 10000000; // 1 second in 100ns units
    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
        requestedDuration,
        0,
        mixFormat,
        nullptr
    );

    CoTaskMemFree(mixFormat);
    if (FAILED(hr)) 
    {
        CloseHandle(m_audioReadyEvent);
        m_audioReadyEvent = nullptr;
        return hr;
    }

    // Set the event handle for buffer notifications
    hr = m_audioClient->SetEventHandle(m_audioReadyEvent);
    if (FAILED(hr))
    {
        CloseHandle(m_audioReadyEvent);
        m_audioReadyEvent = nullptr;
        return hr;
    }

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
        constexpr DWORD THREAD_STOP_TIMEOUT_MS = 5000;
        WaitForSingleObject(m_captureThread, THREAD_STOP_TIMEOUT_MS);
        CloseHandle(m_captureThread);
        m_captureThread = nullptr;
    }

    if (m_audioClient)
    {
        m_audioClient->Stop();
    }

    if (m_audioReadyEvent)
    {
        CloseHandle(m_audioReadyEvent);
        m_audioReadyEvent = nullptr;
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

    // Pre-allocate silent buffer to maximum expected size to avoid allocations in capture loop
    // Assuming max 48kHz stereo * 32-bit float * 1 second buffer = 384KB
    constexpr size_t MAX_BUFFER_SIZE = 48000 * 4 * 4; // 48kHz * 4 bytes/sample * stereo * safety margin
    std::vector<BYTE> silentBuffer(MAX_BUFFER_SIZE, 0);
    
    // Pre-allocate conversion buffer for float to PCM conversion
    std::vector<BYTE> pcmBuffer(MAX_BUFFER_SIZE, 0);
    
    // Check if we need to convert float to PCM (WASAPI often returns float)
    bool isFloatFormat = false;
    if (m_audioFormat->wFormatTag == WAVE_FORMAT_IEEE_FLOAT)
    {
        isFloatFormat = true;
    }
    else if (m_audioFormat->wFormatTag == WAVE_FORMAT_EXTENSIBLE)
    {
        WAVEFORMATEXTENSIBLE* formatEx = (WAVEFORMATEXTENSIBLE*)m_audioFormat;
        if (IsEqualGUID(formatEx->SubFormat, KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
        {
            isFloatFormat = true;
        }
    }
    
    // Get QPC frequency once
    LARGE_INTEGER qpcFreq;
    QueryPerformanceFrequency(&qpcFreq);

    // Wait handles for event-driven capture
    HANDLE waitHandles[2] = { m_stopEvent, m_audioReadyEvent };
    constexpr DWORD STOP_EVENT_INDEX = 0;
    constexpr DWORD AUDIO_READY_INDEX = 1;
    constexpr DWORD WAIT_TIMEOUT_MS = 5000; // 5 second timeout to prevent indefinite hang

    while (m_isCapturing)
    {
        // Wait for stop event or audio ready event with timeout
        DWORD waitResult = WaitForMultipleObjects(2, waitHandles, FALSE, WAIT_TIMEOUT_MS);
        
        if (waitResult == WAIT_OBJECT_0 + STOP_EVENT_INDEX)
        {
            break; // Stop event signaled
        }
        
        if (waitResult == WAIT_OBJECT_0 + AUDIO_READY_INDEX)
        {
            // Audio ready event was signaled - process all available packets
        }
        else if (waitResult == WAIT_TIMEOUT)
        {
            // Timeout - continue waiting (audio system might be idle)
            continue;
        }
        else
        {
            // WAIT_FAILED or WAIT_ABANDONED - unexpected error
            // Log error but continue trying
            continue;
        }

        // Process all available audio packets
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

            // Calculate relative timestamp using QPC position for accurate synchronization
            LONGLONG relativeTimestamp = 0;
            if (m_firstAudioTimestamp == 0)
            {
                m_firstAudioTimestamp = qpcPosition;
            }
            
            // Convert QPC to 100ns units relative to start
            LONGLONG qpcDelta = qpcPosition - m_firstAudioTimestamp;
            relativeTimestamp = (qpcDelta * 10000000LL) / qpcFreq.QuadPart;

            // Prepare sample data
            BYTE* sampleData = data;
            UINT32 dataSize = numFramesAvailable * m_audioFormat->nBlockAlign;
            
            // Handle silence flag - write actual silent audio instead of skipping
            if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0)
            {
                // Use pre-allocated silent buffer (no allocation in capture loop)
                if (dataSize <= silentBuffer.size())
                {
                    sampleData = silentBuffer.data();
                }
                // If size exceeds buffer, skip this packet (very unlikely)
                else
                {
                    m_captureClient->ReleaseBuffer(numFramesAvailable);
                    hr = m_captureClient->GetNextPacketSize(&packetLength);
                    if (FAILED(hr)) break;
                    continue;
                }
            }
            else if (isFloatFormat && data)
            {
                // Convert float32 to int16 PCM for proper audio encoding
                // This is the KEY FIX for the static issue
                const float* floatData = reinterpret_cast<const float*>(data);
                int16_t* pcmData = reinterpret_cast<int16_t*>(pcmBuffer.data());
                UINT32 numSamples = numFramesAvailable * m_audioFormat->nChannels;
                UINT32 pcmDataSize = numSamples * sizeof(int16_t);
                
                if (pcmDataSize <= pcmBuffer.size())
                {
                    // Convert float [-1.0, 1.0] to int16 [-32768, 32767]
                    for (UINT32 i = 0; i < numSamples; ++i)
                    {
                        float sample = floatData[i];
                        // Clamp to valid range
                        if (sample > 1.0f) sample = 1.0f;
                        if (sample < -1.0f) sample = -1.0f;
                        // Convert to 16-bit integer
                        pcmData[i] = static_cast<int16_t>(sample * 32767.0f);
                    }
                    sampleData = pcmBuffer.data();
                    dataSize = pcmDataSize;
                }
                else
                {
                    // Buffer too small, skip this packet
                    m_captureClient->ReleaseBuffer(numFramesAvailable);
                    hr = m_captureClient->GetNextPacketSize(&packetLength);
                    if (FAILED(hr)) break;
                    continue;
                }
            }

            // Call the callback with audio data (silent, converted, or actual)
            // This maintains stream continuity and prevents gaps/static
            if (m_onAudioSample && numFramesAvailable > 0 && sampleData)
            {
                m_onAudioSample(sampleData, dataSize, relativeTimestamp);
            }

            m_captureClient->ReleaseBuffer(numFramesAvailable);

            hr = m_captureClient->GetNextPacketSize(&packetLength);
            if (FAILED(hr)) break;
        }
    }

    CoUninitialize();
}
