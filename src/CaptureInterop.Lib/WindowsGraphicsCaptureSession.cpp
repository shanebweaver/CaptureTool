#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "IAudioCaptureSourceFactory.h"
#include "IVideoCaptureSourceFactory.h"
#include "IVideoCaptureSource.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "IMediaClockFactory.h"

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

    // Create clock
    if (m_mediaClockFactory)
    {
        m_mediaClock = m_mediaClockFactory->CreateClock();
    }
    if (!m_mediaClock)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Create audio input source with clock reader
    if (m_audioCaptureSourceFactory)
    {
        m_audioInputSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(m_mediaClock.get());
    }
    if (!m_audioInputSource)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Connect the audio source as the clock advancer so it drives the timeline
    m_mediaClock->SetClockAdvancer(m_audioInputSource.get());

    // Start the media clock early
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    LONGLONG startQpc = qpc.QuadPart;
    if (m_mediaClock)
    {
        m_mediaClock->Start(startQpc);
    }

    // Create video capture source with clock reader
    if (m_videoCaptureSourceFactory)
    {
        m_videoCaptureSource = m_videoCaptureSourceFactory->CreateVideoCaptureSource(m_config, m_mediaClock.get());
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

    // Initialize sink writer with video and audio streams
    if (!InitializeSinkWriter(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Start audio capture
    if (!StartAudioCapture(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Set the sink writer on video capture source
    m_videoCaptureSource->SetSinkWriter(&m_sinkWriter);

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

bool WindowsGraphicsCaptureSession::InitializeSinkWriter(HRESULT* outHr)
{
    HRESULT hr = S_OK;
    
    // Get video dimensions and device
    UINT32 width = m_videoCaptureSource->GetWidth();
    UINT32 height = m_videoCaptureSource->GetHeight();
    ID3D11Device* device = static_cast<WindowsDesktopVideoCaptureSource*>(m_videoCaptureSource.get())->GetDevice();
    
    // Initialize video stream
    // TODO: Use m_config.frameRate, m_config.videoBitrate, and m_config.audioBitrate when implementing encoder settings
    if (!m_sinkWriter.Initialize(m_config.outputPath, device, width, height, &hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize audio stream if audio source is available
    if (m_audioInputSource && m_audioInputSource->Initialize(&hr))
    {
        WAVEFORMATEX* audioFormat = m_audioInputSource->GetFormat();
        if (audioFormat)
        {
            if (!m_sinkWriter.InitializeAudioStream(audioFormat, &hr))
            {
                if (outHr) *outHr = hr;
                return false;
            }
            
            // Set the sink writer on audio input source
            m_audioInputSource->SetSinkWriter(&m_sinkWriter);
        }
    }
    
    if (outHr) *outHr = S_OK;
    return true;
}

bool WindowsGraphicsCaptureSession::StartAudioCapture(HRESULT* outHr)
{
    // Only start if audio source exists and has a format (meaning it was initialized)
    if (!m_audioInputSource || !m_audioInputSource->GetFormat())
    {
        if (outHr) *outHr = S_OK;
        return true;
    }
    
    // Enable/disable audio based on config
    m_audioInputSource->SetEnabled(m_config.audioEnabled);
    
    // Start audio capture
    HRESULT hr = S_OK;
    if (!m_audioInputSource->Start(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
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
