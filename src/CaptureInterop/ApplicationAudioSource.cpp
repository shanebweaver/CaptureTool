#include "pch.h"
#include "ApplicationAudioSource.h"

ApplicationAudioSource::ApplicationAudioSource()
    : m_ref(1)
{
    QueryPerformanceFrequency(&m_qpcFrequency);
}

ApplicationAudioSource::~ApplicationAudioSource()
{
    Cleanup();
}

void ApplicationAudioSource::SetProcessId(DWORD processId)
{
    m_processId = processId;
}

DWORD ApplicationAudioSource::GetProcessId() const
{
    return m_processId;
}

bool ApplicationAudioSource::IsSupported()
{
    return WindowsVersionHelper::IsWindows11_22H2OrLater();
}

WAVEFORMATEX* ApplicationAudioSource::GetFormat() const
{
    return m_format;
}

void ApplicationAudioSource::SetAudioCallback(AudioSampleCallback callback)
{
    m_callback = callback;
}

void ApplicationAudioSource::SetEnabled(bool enabled)
{
    if (m_isEnabled != enabled)
    {
        m_isEnabled = enabled;
        if (!enabled)
        {
            m_wasDisabled = true;
        }
    }
}

bool ApplicationAudioSource::IsEnabled() const
{
    return m_isEnabled;
}

bool ApplicationAudioSource::Initialize()
{
    if (m_isInitialized)
    {
        return true; // Already initialized
    }

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE)
    {
        return false;
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
        return false;
    }

    // Get default render device (for loopback capture)
    hr = m_deviceEnumerator->GetDefaultAudioEndpoint(
        eRender,
        eConsole,
        m_device.put()
    );
    if (FAILED(hr))
    {
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
        return false;
    }

    // Get the mix format
    hr = m_audioClient->GetMixFormat(&m_format);
    if (FAILED(hr))
    {
        return false;
    }

    // Initialize audio client in loopback mode
    // NOTE: Full per-process capture would require IAudioClient3 with 
    // AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_PROCESS_LOOPBACK
    // and AudioClientProperties.Options = AUDCLNT_STREAMOPTIONS_MATCH_FORMAT
    // This requires Windows 11 22H2+ and is more complex
    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        AUDCLNT_STREAMFLAGS_LOOPBACK,
        0,
        0,
        m_format,
        nullptr
    );
    if (FAILED(hr))
    {
        return false;
    }

    // Get the capture client
    hr = m_audioClient->GetService(
        __uuidof(IAudioCaptureClient),
        m_captureClient.put_void()
    );
    if (FAILED(hr))
    {
        return false;
    }

    // Pre-allocate silent buffer
    if (m_format)
    {
        UINT32 bufferSize = (m_format->nSamplesPerSec / 100) * m_format->nBlockAlign;
        m_silentBuffer.resize(bufferSize, 0);
    }

    m_isInitialized = true;
    return true;
}

bool ApplicationAudioSource::Start()
{
    if (!m_isInitialized)
    {
        return false;
    }

    if (m_isRunning)
    {
        return true;
    }

    if (!m_callback)
    {
        return false;
    }

    HRESULT hr = m_audioClient->Start();
    if (FAILED(hr))
    {
        return false;
    }

    // Record start time
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_startQpc = qpc.QuadPart;
    
    // Reset audio timestamp counter
    m_nextAudioTimestamp = 0;

    // Start capture thread
    m_isRunning = true;
    m_captureThread = std::thread(&ApplicationAudioSource::CaptureThreadProc, this);

    return true;
}

void ApplicationAudioSource::Stop()
{
    if (!m_isRunning)
    {
        return;
    }

    m_isRunning = false;
    
    if (m_captureThread.joinable())
    {
        m_captureThread.join();
    }
    
    if (m_audioClient)
    {
        m_audioClient->Stop();
    }
}

bool ApplicationAudioSource::IsRunning() const
{
    return m_isRunning;
}

ULONG ApplicationAudioSource::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG ApplicationAudioSource::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}

void ApplicationAudioSource::CaptureThreadProc()
{
    // Set thread priority to above normal
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
    
    while (m_isRunning)
    {
        BYTE* pData = nullptr;
        UINT32 numFramesAvailable = 0;
        DWORD flags = 0;
        UINT64 devicePosition = 0;
        UINT64 qpcPosition = 0;

        HRESULT hr = m_captureClient->GetBuffer(
            &pData,
            &numFramesAvailable,
            &flags,
            &devicePosition,
            &qpcPosition
        );

        if (SUCCEEDED(hr) && numFramesAvailable > 0)
        {
            const LONGLONG TICKS_PER_SECOND = 10000000LL;
            
            // If this is the first write after being disabled, sync timestamp
            if (m_nextAudioTimestamp == 0 || m_wasDisabled)
            {
                LARGE_INTEGER qpc;
                QueryPerformanceCounter(&qpc);
                LONGLONG currentQpc = qpc.QuadPart;
                LONGLONG elapsedQpc = currentQpc - m_startQpc;
                
                m_nextAudioTimestamp = (elapsedQpc * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
                m_wasDisabled = false;
                
                m_samplesToSkip = 5;
            }
            
            // Skip samples if draining stale buffer
            if (m_samplesToSkip > 0)
            {
                m_samplesToSkip--;
                m_captureClient->ReleaseBuffer(numFramesAvailable);
                continue;
            }
            
            LONGLONG timestamp = m_nextAudioTimestamp;
            LONGLONG duration = (numFramesAvailable * TICKS_PER_SECOND) / m_format->nSamplesPerSec;
            
            // Handle silence or disabled state
            BYTE* pAudioData = pData;
            if (!m_isEnabled || (flags & AUDCLNT_BUFFERFLAGS_SILENT))
            {
                UINT32 bufferSize = numFramesAvailable * m_format->nBlockAlign;
                if (m_silentBuffer.size() < bufferSize)
                {
                    m_silentBuffer.resize(bufferSize, 0);
                }
                else
                {
                    memset(m_silentBuffer.data(), 0, bufferSize);
                }
                pAudioData = m_silentBuffer.data();
            }
            
            // Call the callback
            if (m_callback)
            {
                m_callback(pAudioData, numFramesAvailable, timestamp);
            }
            
            m_nextAudioTimestamp += duration;

            m_captureClient->ReleaseBuffer(numFramesAvailable);
        }
        else
        {
            // No audio data - sleep to avoid busy-waiting
            Sleep(10);
        }
    }
}

void ApplicationAudioSource::Cleanup()
{
    Stop();
    
    if (m_format)
    {
        CoTaskMemFree(m_format);
        m_format = nullptr;
    }
}
