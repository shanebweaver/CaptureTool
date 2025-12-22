#include "pch.h"
#include "AudioCaptureHandler.h"
#include "IAudioCaptureSource.h"
#include "IMediaClockWriter.h"
#include "IMediaClockReader.h"

#include <mmreg.h>
#include <strsafe.h>
#include <Audioclient.h>
#include <Windows.h>
#include <thread>

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
    if (!m_device.Initialize(loopback, outHr))
    {
        return false;
    }
    
    // Cache the sample rate for efficient access during capture
    WAVEFORMATEX* format = m_device.GetFormat();
    if (format)
    {
        m_sampleRate = format->nSamplesPerSec;
    }
    
    return true;
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
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
    
    // Validate that we have the required components before starting
    if (!m_clockWriter || m_sampleRate == 0)
    {
        return;
    }
    
    LONGLONG lastTimestamp = 0;
    const LONGLONG TICKS_PER_SECOND = 10000000LL;
    const UINT32 SLEEP_DURATION_MS = 10;
    const UINT32 VIRTUAL_FRAMES_PER_SLEEP = (m_sampleRate * SLEEP_DURATION_MS) / 1000;
    
    // Track last clock advancement time for more accurate timing
    LARGE_INTEGER qpcFreq, lastAdvanceQpc, currentQpc;
    QueryPerformanceFrequency(&qpcFreq);
    QueryPerformanceCounter(&lastAdvanceQpc);
    
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
            WAVEFORMATEX* format = m_device.GetFormat();
            if (!format)
            {
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            LONGLONG duration = (framesRead * TICKS_PER_SECOND) / m_sampleRate;
            
            if (m_audioSampleReadyCallback)
            {
                LONGLONG timestamp = 0;
                if (m_clockReader && m_clockReader->IsRunning())
                {
                    timestamp = m_clockReader->GetCurrentTime();
                }
                else
                {
                    timestamp = lastTimestamp + duration;
                }
                
                if (m_wasDisabled)
                {
                    m_wasDisabled = false;
                    m_samplesToSkip = 5;
                }
                
                if (m_samplesToSkip > 0)
                {
                    m_samplesToSkip--;
                    m_device.ReleaseBuffer(framesRead);
                    
                    // Still advance clock even when skipping samples
                    m_clockWriter->AdvanceByAudioSamples(framesRead, m_sampleRate);
                    QueryPerformanceCounter(&lastAdvanceQpc);
                    continue;
                }
                
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
                
                AudioSampleReadyEventArgs args{};
                args.pData = pAudioData;
                args.numFrames = framesRead;
                args.timestamp = timestamp;
                args.pFormat = format;
                
                m_audioSampleReadyCallback(args);
                
                lastTimestamp = timestamp;
            }

            // Always advance the clock when frames are processed
            m_clockWriter->AdvanceByAudioSamples(framesRead, m_sampleRate);
            QueryPerformanceCounter(&lastAdvanceQpc);

            m_device.ReleaseBuffer(framesRead);
        }
        else
        {
            // No audio data available immediately
            // Check if we've been waiting too long and need to advance the clock
            QueryPerformanceCounter(&currentQpc);
            LONGLONG qpcElapsed = currentQpc.QuadPart - lastAdvanceQpc.QuadPart;
            LONGLONG ticksElapsed = (qpcElapsed * TICKS_PER_SECOND) / qpcFreq.QuadPart;
            
            // If more than 10ms has elapsed since last advancement, advance the clock
            if (ticksElapsed >= 100000LL) // 10ms in 100ns ticks
            {
                // Calculate frames equivalent to elapsed time
                UINT32 virtualFrames = (UINT32)((ticksElapsed * m_sampleRate) / TICKS_PER_SECOND);
                if (virtualFrames > 0)
                {
                    m_clockWriter->AdvanceByAudioSamples(virtualFrames, m_sampleRate);
                    lastAdvanceQpc = currentQpc;
                }
            }
            
            // Sleep briefly to avoid busy-waiting
            Sleep(1); // Shorter sleep to check more frequently
        }
    }
}
