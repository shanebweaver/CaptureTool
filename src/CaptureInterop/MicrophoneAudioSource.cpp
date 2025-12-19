#include "pch.h"
#include "MicrophoneAudioSource.h"

MicrophoneAudioSource::MicrophoneAudioSource()
    : m_ref(1)
{
    QueryPerformanceFrequency(&m_qpcFrequency);
}

MicrophoneAudioSource::~MicrophoneAudioSource()
{
    Cleanup();
}

void MicrophoneAudioSource::SetDeviceId(const std::wstring& deviceId)
{
    m_deviceId = deviceId;
}

std::wstring MicrophoneAudioSource::GetDeviceId() const
{
    return m_deviceId;
}

WAVEFORMATEX* MicrophoneAudioSource::GetFormat() const
{
    return m_device.GetFormat();
}

void MicrophoneAudioSource::SetAudioCallback(AudioSampleCallback callback)
{
    m_callback = callback;
}

void MicrophoneAudioSource::SetEnabled(bool enabled)
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

bool MicrophoneAudioSource::IsEnabled() const
{
    return m_isEnabled;
}

bool MicrophoneAudioSource::Initialize()
{
    if (m_isInitialized)
    {
        return true; // Already initialized
    }

    HRESULT hr = S_OK;
    
    // Initialize in capture mode (false = capture endpoint, not loopback)
    // Pass device ID for specific device selection (nullptr/empty for default)
    const wchar_t* deviceIdPtr = m_deviceId.empty() ? nullptr : m_deviceId.c_str();
    if (!m_device.Initialize(false, &hr, deviceIdPtr))
    {
        return false;
    }

    // Pre-allocate silent buffer
    WAVEFORMATEX* format = m_device.GetFormat();
    if (format)
    {
        // Allocate for typical buffer size (10ms of audio)
        UINT32 bufferSize = (format->nSamplesPerSec / 100) * format->nBlockAlign;
        m_silentBuffer.resize(bufferSize, 0);
    }

    m_isInitialized = true;
    return true;
}

bool MicrophoneAudioSource::Start()
{
    if (!m_isInitialized)
    {
        return false; // Must initialize first
    }

    if (m_isRunning)
    {
        return true; // Already running
    }

    if (!m_callback)
    {
        return false; // No callback set
    }

    HRESULT hr = S_OK;
    
    // Start the audio capture device
    if (!m_device.Start(&hr))
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
    m_captureThread = std::thread(&MicrophoneAudioSource::CaptureThreadProc, this);

    return true;
}

void MicrophoneAudioSource::Stop()
{
    if (!m_isRunning)
    {
        return; // Not running
    }

    m_isRunning = false;
    
    if (m_captureThread.joinable())
    {
        m_captureThread.join();
    }
    
    m_device.Stop();
}

bool MicrophoneAudioSource::IsRunning() const
{
    return m_isRunning;
}

ULONG MicrophoneAudioSource::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG MicrophoneAudioSource::Release()
{
    ULONG ref = InterlockedDecrement(&m_ref);
    if (ref == 0)
    {
        delete this;
    }
    return ref;
}

void MicrophoneAudioSource::CaptureThreadProc()
{
    // Set thread priority to above normal (not TIME_CRITICAL to avoid starving UI)
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
    
    while (m_isRunning)
    {
        BYTE* pData = nullptr;
        UINT32 numFramesAvailable = 0;
        DWORD flags = 0;
        UINT64 devicePosition = 0;
        UINT64 qpcPosition = 0;

        UINT32 framesRead = m_device.ReadSamples(
            &pData,
            &numFramesAvailable,
            &flags,
            &devicePosition,
            &qpcPosition
        );

        if (framesRead > 0)
        {
            // Get audio format for duration calculation
            WAVEFORMATEX* format = m_device.GetFormat();
            if (!format)
            {
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            const LONGLONG TICKS_PER_SECOND = 10000000LL;  // 100ns ticks per second
            
            // If this is the first write after being disabled, sync timestamp
            if (m_nextAudioTimestamp == 0 || m_wasDisabled)
            {
                LARGE_INTEGER qpc;
                QueryPerformanceCounter(&qpc);
                LONGLONG currentQpc = qpc.QuadPart;
                LONGLONG elapsedQpc = currentQpc - m_startQpc;
                
                // Convert QPC ticks to 100ns units
                m_nextAudioTimestamp = (elapsedQpc * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
                m_wasDisabled = false;
                
                // Skip next few samples to drain stale buffer data
                m_samplesToSkip = 5;
            }
            
            // Skip samples if draining stale buffer
            if (m_samplesToSkip > 0)
            {
                m_samplesToSkip--;
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            // Use accumulated timestamp to prevent overlapping samples
            LONGLONG timestamp = m_nextAudioTimestamp;
            
            // Calculate duration based on actual audio frame count
            LONGLONG duration = (framesRead * TICKS_PER_SECOND) / format->nSamplesPerSec;
            
            // When audio is disabled or silent, send silent samples
            BYTE* pAudioData = pData;
            if (!m_isEnabled || (flags & AUDCLNT_BUFFERFLAGS_SILENT))
            {
                UINT32 bufferSize = framesRead * format->nBlockAlign;
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
            
            // Call the callback with audio data
            if (m_callback)
            {
                m_callback(pAudioData, framesRead, timestamp);
            }
            
            // Always advance timestamp
            m_nextAudioTimestamp += duration;

            m_device.ReleaseBuffer(framesRead);
        }
        else
        {
            // No audio data - sleep to avoid busy-waiting
            Sleep(10);
        }
    }
}

void MicrophoneAudioSource::Cleanup()
{
    Stop();
}
