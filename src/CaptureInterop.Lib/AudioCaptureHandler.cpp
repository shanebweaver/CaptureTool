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

    // Get audio format to create MediaClock with correct sample rate
    WAVEFORMATEX* format = m_device.GetFormat();
    if (!format) {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Create the MediaClock with the audio device's sample rate
    // This becomes the master clock for all media sources
    m_mediaClock = std::make_unique<MediaClock>(
        MediaClock::SampleRate{format->nSamplesPerSec}
    );

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
        m_mediaClock.reset(); // Clear the clock to reset state
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
            // Advance the master clock first (audio is the law)
            // This must happen before any timestamp usage to ensure clock is up-to-date
            if (m_mediaClock) {
                m_mediaClock->Advance(framesRead);
            }
            
            // Get audio format for duration calculation
            WAVEFORMATEX* format = m_device.GetFormat();
            if (!format)
            {
                // No format available, skip this sample and release buffer
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            const LONGLONG TICKS_PER_SECOND = 10000000LL;  // 100ns ticks per second
            
            // Always process audio samples to maintain timeline continuity
            // Process even when AUDCLNT_BUFFERFLAGS_SILENT is set to prevent tight loop that freezes UI
            if (m_sinkWriter)
            {
                // If this is the first write after being disabled, sync timestamp to current recording time
                // and prepare to skip several samples to drain any stale buffer data
                if (m_nextAudioTimestamp == 0 || m_wasDisabled)
                {
                    // Use MediaClock as the authoritative timestamp source
                    // First buffer after start/resume = MediaClock's current time
                    if (m_mediaClock) {
                        m_nextAudioTimestamp = m_mediaClock->CurrentTime().ticks;
                    } else {
                        // Fallback to QPC if MediaClock not available (shouldn't happen)
                        LARGE_INTEGER qpc;
                        QueryPerformanceCounter(&qpc);
                        LONGLONG currentQpc = qpc.QuadPart;
                        LONGLONG elapsedQpc = currentQpc - m_startQpc;
                        m_nextAudioTimestamp = (elapsedQpc * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
                    }
                    m_wasDisabled = false;
                    
                    // Skip next few samples to fully drain stale buffer data
                    // WASAPI typically buffers 10-30ms of audio, which is 3-5 samples at 10ms per sample
                    m_samplesToSkip = 5;
                }
                
                // Skip samples if we're draining stale buffer data
                if (m_samplesToSkip > 0)
                {
                    m_samplesToSkip--;
                    m_device.ReleaseBuffer(framesRead);
                    continue;
                }
                
                // Use accumulated timestamp to prevent overlapping samples
                // This is crucial: using wall clock time would create overlaps since
                // the capture loop runs faster (1-2ms) than audio buffer duration (10ms)
                LONGLONG timestamp = m_nextAudioTimestamp;
                
                // Calculate duration based on actual audio frame count
                // This gives the exact playback duration of this sample
                LONGLONG duration = (framesRead * TICKS_PER_SECOND) / format->nSamplesPerSec;
                
                // When audio is disabled or silent, write silent samples to maintain timeline continuity
                // This prevents video from skipping ahead and prevents UI freeze from tight loops
                BYTE* pAudioData = pData;
                if (!m_isEnabled || (flags & AUDCLNT_BUFFERFLAGS_SILENT))
                {
                    // Reuse or resize silent buffer for efficiency
                    UINT32 bufferSize = framesRead * format->nBlockAlign;
                    if (m_silentBuffer.size() < bufferSize)
                    {
                        m_silentBuffer.resize(bufferSize, 0);
                    }
                    else
                    {
                        // Zero out the buffer for reuse
                        memset(m_silentBuffer.data(), 0, bufferSize);
                    }
                    pAudioData = m_silentBuffer.data();
                }
                
                HRESULT hr = m_sinkWriter->WriteAudioSample(pAudioData, framesRead, timestamp);
                
                // If write fails, stop trying to write more samples to prevent blocking
                if (FAILED(hr))
                {
                    // Disable audio writing to prevent further blocking
                    m_isEnabled = false;
                    m_wasDisabled = true;
                }
                
                // Advance timestamp for next buffer
                // This works in tandem with MediaClock: the clock tracks total samples,
                // while this timestamp tracks the write position in the output stream
                m_nextAudioTimestamp += duration;
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
