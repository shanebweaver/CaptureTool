#include "pch.h"
#include "AudioCaptureHandler.h"
#include "IAudioCaptureSource.h"
#include "IMediaClockWriter.h"
#include "IMediaClockReader.h"

AudioCaptureHandler::AudioCaptureHandler(IMediaClockReader* clockReader)
    : m_clockReader(clockReader)
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
    
    LONGLONG lastTimestamp = 0;
    
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
            
            // Calculate duration based on actual audio frame count
            LONGLONG duration = (framesRead * TICKS_PER_SECOND) / format->nSamplesPerSec;
            
            // Always process audio samples to maintain timeline continuity
            if (m_audioSampleReadyCallback)
            {
                // Get current timestamp from media clock
                LONGLONG timestamp = 0;
                if (m_clockReader && m_clockReader->IsRunning())
                {
                    timestamp = m_clockReader->GetCurrentTime();
                }
                else
                {
                    // Fallback: use last timestamp + duration if clock not available
                    timestamp = lastTimestamp + duration;
                }
                
                // If this is the first write after being disabled, prepare to skip several samples
                // to drain any stale buffer data
                if (m_wasDisabled)
                {
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
                
                // Fire the audio sample ready event
                AudioSampleReadyEventArgs args;
                args.pData = pAudioData;
                args.numFrames = framesRead;
                args.timestamp = timestamp;
                args.pFormat = format;
                
                m_audioSampleReadyCallback(args);
                
                lastTimestamp = timestamp;
            }

            // Advance the media clock based on audio samples processed
            // This is done regardless of whether audio is written to maintain accurate timeline
            if (m_clockWriter && format)
            {
                m_clockWriter->AdvanceByAudioSamples(framesRead, format->nSamplesPerSec);
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
