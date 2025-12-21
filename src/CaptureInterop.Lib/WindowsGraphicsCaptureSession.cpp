#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "FrameArrivedHandler.h"
#include "IAudioCaptureSourceFactory.h"
#include "IVideoCaptureSourceFactory.h"
#include "IVideoCaptureSource.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "IMediaClockFactory.h"
#include "WindowsGraphicsCaptureHelpers.h"

using namespace WindowsGraphicsCaptureHelpers;

WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    IMediaClockFactory* mediaClockFactory,
    IAudioCaptureSourceFactory* audioCaptureSourceFactory,
    IVideoCaptureSourceFactory* videoCaptureSourceFactory)
    : m_config(config)
    , m_mediaClockFactory(mediaClockFactory)
    , m_audioCaptureSourceFactory(audioCaptureSourceFactory)
    , m_videoCaptureSourceFactory(videoCaptureSourceFactory)
    , m_audioInputSource(nullptr)
    , m_videoCaptureSource(nullptr)
    , m_isActive(false)
{
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
}

bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Simplify into these steps:
    // 1. Create clock
    // 2. Create audio input source
    // 3. Create video capture source
    // 4. Configure clock using audio source
	// 5. Start the clock and capture on both sources

    // 1. Create clock
    if (m_mediaClockFactory)
    {
        m_mediaClock = m_mediaClockFactory->CreateClock();
    }
    if (!m_mediaClock)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // 2. Create audio input source
    if (m_audioCaptureSourceFactory)
    {
        m_audioInputSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource();
    }
    if (!m_audioInputSource)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // 3. Create video capture source
    if (m_videoCaptureSourceFactory)
    {
        m_videoCaptureSource = m_videoCaptureSourceFactory->CreateVideoCaptureSource(m_config);
    }
    if (!m_videoCaptureSource)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Initialize video capture source
    if (!m_videoCaptureSource->Initialize(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize video sink writer using dimensions from video source
    UINT32 width = m_videoCaptureSource->GetWidth();
    UINT32 height = m_videoCaptureSource->GetHeight();
    ID3D11Device* device = static_cast<WindowsDesktopVideoCaptureSource*>(m_videoCaptureSource.get())->GetDevice();
    
    // TODO: Use m_config.frameRate, m_config.videoBitrate, and m_config.audioBitrate when implementing encoder settings
    if (!m_sinkWriter.Initialize(m_config.outputPath, device, width, height, &hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize audio capture device
    if (m_audioInputSource && m_audioInputSource->Initialize(&hr))
    {
        // Initialize audio stream on sink writer
        WAVEFORMATEX* audioFormat = m_audioInputSource->GetFormat();
        if (audioFormat && m_sinkWriter.InitializeAudioStream(audioFormat, &hr))
        {
            // Set the sink writer on audio input source so it can write samples
            m_audioInputSource->SetSinkWriter(&m_sinkWriter);
                    
            // Start audio capture
            m_audioInputSource->SetEnabled(m_config.audioEnabled);
            hr = m_audioInputSource->Start(&hr);
            if (FAILED(hr))
            {
                if (outHr) *outHr = hr;
                return false;
            }
        }
    }
    
    // Set the sink writer on video capture source
    m_videoCaptureSource->SetSinkWriter(&m_sinkWriter);

    // Connect the audio source as the clock advancer so it drives the timeline
    m_mediaClock->SetClockAdvancer(m_audioInputSource.get());

    // Start the media clock
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    LONGLONG startQpc = qpc.QuadPart;
    if (m_mediaClock)
    {
        m_mediaClock->Start(startQpc);
    }

    // Start video capture
    if (!m_videoCaptureSource->Start(&hr))
    {
        // If video capture fails, stop audio if it was started
        if (m_audioInputSource && m_audioInputSource->IsRunning())
        {
            m_audioInputSource->Stop();
        }
        if (outHr) *outHr = hr;
        return false;
    }

    m_isActive = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::Stop()
{
    if (!m_isActive)
    {
        return;
    }

    // Stop the clock
    if (m_mediaClock)
    {
        m_mediaClock->Pause();
    }

    // Stop video capture first
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->Stop();
    }

    // Stop audio capture
    if (m_audioInputSource)
    {
        m_audioInputSource->Stop();
    }

    // Finalize MP4 file after both streams have stopped
    m_sinkWriter.Finalize();

    // Reset the clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    m_isActive = false;
}

void WindowsGraphicsCaptureSession::ToggleAudioCapture(bool enabled)
{
    // Only toggle if audio capture is currently running
    if (m_audioInputSource && m_audioInputSource->IsRunning())
    {
        m_audioInputSource->SetEnabled(enabled);
    }
}
