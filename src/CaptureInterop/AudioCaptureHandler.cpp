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
    // Set thread priority to time-critical for audio capture
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_TIME_CRITICAL);
    
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
            // Calculate relative timestamp in 100-nanosecond units
            // Use current QPC time instead of device position for better synchronization
            // The qpcPosition from WASAPI represents when audio was captured by device,
            // which can be in the past (buffered), causing audio to speed up
            LARGE_INTEGER currentQpc;
            if (!QueryPerformanceCounter(&currentQpc))
            {
                // QueryPerformanceCounter failed, skip this sample
                m_device.ReleaseBuffer(framesRead);
                continue;
            }
            
            LONGLONG relativeQpc = currentQpc.QuadPart - m_startQpc;
            LONGLONG timestamp = (relativeQpc * 10000000) / m_qpcFrequency.QuadPart;

            // Write audio sample to MP4SinkWriter if available and not silent
            // Note: We still process silent frames but Media Foundation might optimize them
            if (m_sinkWriter && !(flags & AUDCLNT_BUFFERFLAGS_SILENT))
            {
                HRESULT hr = m_sinkWriter->WriteAudioSample(pData, framesRead, timestamp);
                // Note: We don't fail on audio write errors to avoid stopping the entire capture
                // Audio frames may be dropped, but video capture continues
                (void)hr; // Explicitly ignore return value after checking
            }

            m_device.ReleaseBuffer(framesRead);
        }

        // Very short sleep to yield CPU but remain responsive
        // This prevents busy-waiting while keeping latency low
        Sleep(1);
    }
}
