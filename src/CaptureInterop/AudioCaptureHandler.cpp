#include "pch.h"
#include "AudioCaptureHandler.h"
#include "MP4SinkWriter.h"

AudioCaptureHandler::AudioCaptureHandler()
{
    QueryPerformanceFrequency(&m_qpcFrequency);
}

AudioCaptureHandler::~AudioCaptureHandler()
{
    Stop();
}

// ============================================================================
// Initialization and Lifecycle
// ============================================================================

bool AudioCaptureHandler::Initialize(bool loopback, HRESULT* outHr)
{
    return m_device.Initialize(loopback, outHr);
}

bool AudioCaptureHandler::Start(HRESULT* outHr)
{
    if (m_isRunning)
    {
        if (outHr) *outHr = E_NOT_VALID_STATE;
        return false;
    }

    if (!m_device.Start(outHr))
    {
        return false;
    }

    // Synchronize start time with video capture if available
    if (m_sinkWriter)
    {
        LONGLONG sinkStartTime = m_sinkWriter->GetRecordingStartTime();
        if (sinkStartTime != 0)
        {
            // Use existing recording start time from sink writer (video started first)
            m_startQpc = sinkStartTime;
        }
        else
        {
            // Set the recording start time on the sink writer (audio starting first)
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            m_startQpc = qpc.QuadPart;
            m_sinkWriter->SetRecordingStartTime(m_startQpc);
        }
    }
    else
    {
        // No sink writer, use local start time (audio-only mode)
        LARGE_INTEGER qpc;
        QueryPerformanceCounter(&qpc);
        m_startQpc = qpc.QuadPart;
    }

    // Reset audio timestamp counter
    m_nextAudioTimestamp = 0;

    m_isRunning = true;
    m_captureThread = std::thread(&AudioCaptureHandler::CaptureThreadProc, this);

    if (outHr) *outHr = S_OK;
    return true;
}

void AudioCaptureHandler::Stop()
{
    if (m_isRunning)
    {
        m_isRunning = false;
        
        if (m_captureThread.joinable())
        {
            m_captureThread.join();
        }
        
        m_device.Stop();
    }
}

// ============================================================================
// Audio Format Access
// ============================================================================

WAVEFORMATEX* AudioCaptureHandler::GetFormat() const
{
    return m_device.GetFormat();
}

// ============================================================================
// Audio Capture Thread
// ============================================================================

void AudioCaptureHandler::CaptureThreadProc()
{
    // Set thread priority to above normal (not TIME_CRITICAL to avoid starving UI)
    // This provides responsive audio capture while keeping the UI responsive
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
                // No format available, skip this sample and release buffer
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            const LONGLONG TICKS_PER_SECOND = 10000000LL;  // 100ns ticks per second
            
            // Write audio sample to MP4 sink writer (if configured, enabled, and not silent)
            if (m_sinkWriter && m_isEnabled && !(flags & AUDCLNT_BUFFERFLAGS_SILENT))
            {
                // If this is the first write after being disabled, sync timestamp to current recording time
                // This prevents trying to write "old" audio samples that would block the encoder
                if (m_nextAudioTimestamp == 0 || m_wasDisabled)
                {
                    LARGE_INTEGER qpc;
                    QueryPerformanceCounter(&qpc);
                    LONGLONG currentQpc = qpc.QuadPart;
                    LONGLONG elapsedQpc = currentQpc - m_startQpc;
                    
                    // Convert QPC ticks to 100ns units (Media Foundation time)
                    m_nextAudioTimestamp = (elapsedQpc * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
                    m_wasDisabled = false;
                }
                
                // Use accumulated timestamp to prevent overlapping samples
                // This is crucial: using wall clock time would create overlaps since
                // the capture loop runs faster (1-2ms) than audio buffer duration (10ms)
                LONGLONG timestamp = m_nextAudioTimestamp;
                
                // Calculate duration based on actual audio frame count
                // This gives the exact playback duration of this sample
                LONGLONG duration = (framesRead * TICKS_PER_SECOND) / format->nSamplesPerSec;
                
                HRESULT hr = m_sinkWriter->WriteAudioSample(pData, framesRead, timestamp);
                // Don't fail on write errors - allow video capture to continue
                // Audio frames may be dropped, but recording doesn't stop
                (void)hr;
                
                // Advance timestamp for next sample (creates sequential, non-overlapping timeline)
                // Only advance when we actually write audio to prevent timeline gaps
                m_nextAudioTimestamp += duration;
            }
            else if (!m_isEnabled)
            {
                // Track that we were disabled so we can resync when re-enabled
                m_wasDisabled = true;
            }

            m_device.ReleaseBuffer(framesRead);
        }
        else
        {
            // No audio data available - sleep to avoid busy-waiting
            // Longer sleep (10ms) prevents CPU spinning and allows UI thread to run
            // This prevents memory buildup and UI freezes
            Sleep(10);
        }
    }
}
