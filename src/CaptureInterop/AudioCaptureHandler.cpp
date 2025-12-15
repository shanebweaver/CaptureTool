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

    // If we have a sink writer, use its recording start time for synchronization
    // Otherwise, record our own start time (for cases without video)
    if (m_sinkWriter)
    {
        LONGLONG sinkStartTime = m_sinkWriter->GetRecordingStartTime();
        if (sinkStartTime != 0)
        {
            // Use existing recording start time from sink writer
            m_startQpc = sinkStartTime;
        }
        else
        {
            // Set the recording start time on the sink writer
            LARGE_INTEGER qpc;
            QueryPerformanceCounter(&qpc);
            m_startQpc = qpc.QuadPart;
            m_sinkWriter->SetRecordingStartTime(m_startQpc);
        }
    }
    else
    {
        // No sink writer, use local start time
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

WAVEFORMATEX* AudioCaptureHandler::GetFormat() const
{
    return m_device.GetFormat();
}

void AudioCaptureHandler::CaptureThreadProc()
{
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
            LONGLONG relativeQpc = qpcPosition - m_startQpc;
            LONGLONG timestamp = (relativeQpc * 10000000) / m_qpcFrequency.QuadPart;

            // Write audio sample to MP4SinkWriter if available and not silent
            if (m_sinkWriter && !(flags & AUDCLNT_BUFFERFLAGS_SILENT))
            {
                HRESULT hr = m_sinkWriter->WriteAudioSample(pData, framesRead, timestamp);
                // Note: We don't fail on audio write errors to avoid stopping the entire capture
                // Audio frames may be dropped, but video capture continues
                (void)hr; // Explicitly ignore return value after checking
            }

            m_device.ReleaseBuffer(framesRead);
        }

        // Small sleep to prevent busy-waiting
        Sleep(5);
    }
}
