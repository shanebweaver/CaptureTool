#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "IAudioCaptureSourceFactory.h"
#include "IVideoCaptureSourceFactory.h"
#include "IMediaClockFactory.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriterFactory.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "IAudioCaptureSource.h"
#include "IVideoCaptureSource.h"

#include <mmreg.h>
#include <strsafe.h>
#include <d3d11.h>
#include <Windows.h>

WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    IMediaClockFactory* mediaClockFactory,
    IAudioCaptureSourceFactory* audioCaptureSourceFactory,
    IVideoCaptureSourceFactory* videoCaptureSourceFactory,
    IMP4SinkWriterFactory* mp4SinkWriterFactory)
    : m_config(config)
    , m_mediaClockFactory(mediaClockFactory)
    , m_audioCaptureSourceFactory(audioCaptureSourceFactory)
    , m_videoCaptureSourceFactory(videoCaptureSourceFactory)
    , m_mp4SinkWriterFactory(mp4SinkWriterFactory)
    , m_audioCaptureSource(nullptr)
    , m_videoCaptureSource(nullptr)
    , m_sinkWriter(nullptr)
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
        m_audioCaptureSource = m_audioCaptureSourceFactory->CreateAudioCaptureSource(m_mediaClock.get());
    }
    if (!m_audioCaptureSource)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Connect the audio source as the clock advancer so it drives the timeline
    m_mediaClock->SetClockAdvancer(m_audioCaptureSource.get());

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

    // Initialize audio capture source
    if (!m_audioCaptureSource->Initialize(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize sink writer with video and audio streams
    m_sinkWriter = m_mp4SinkWriterFactory->CreateSinkWriter();
    if (!m_sinkWriter)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }
    if (!InitializeSinkWriter(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Set up audio sample callback to write to sink writer and forward to managed layer
    m_audioCaptureSource->SetAudioSampleReadyCallback(
        [this](const AudioSampleReadyEventArgs& args) {
            // Write audio sample to sink writer
            HRESULT hr = m_sinkWriter->WriteAudioSample(args.pData, args.numFrames, args.timestamp);
                
            // If write fails, disable audio to prevent further blocking
            if (FAILED(hr))
            {
                m_audioCaptureSource->SetEnabled(false);
            }

            // Forward to managed layer if callback is set
            if (m_config.audioSampleCallback)
            {
                AudioSampleData sampleData;
                sampleData.pData = args.pData;
                sampleData.numFrames = args.numFrames;
                sampleData.timestamp = args.timestamp;
                
                if (args.pFormat)
                {
                    sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                    sampleData.channels = args.pFormat->nChannels;
                    sampleData.bitsPerSample = args.pFormat->wBitsPerSample;
                }
                else
                {
                    sampleData.sampleRate = 0;
                    sampleData.channels = 0;
                    sampleData.bitsPerSample = 0;
                }
                
                m_config.audioSampleCallback(&sampleData);
            }
        }
    );
    
    // Set up video frame callback to write to sink writer and forward to managed layer
    m_videoCaptureSource->SetVideoFrameReadyCallback(
        [this](const VideoFrameReadyEventArgs& args) {
            // Write video frame to sink writer
            HRESULT hr = m_sinkWriter->WriteFrame(args.pTexture, args.timestamp);
            
            // If write fails, log or handle error
            // Note: We don't stop video capture on write failure to maintain stability
            if (FAILED(hr))
            {
                // Video frame write failed, but continue processing
                // The sink writer will handle errors internally
            }

            // Forward to managed layer if callback is set
            if (m_config.videoFrameCallback)
            {
                VideoFrameData frameData;
                frameData.pTexture = args.pTexture;
                frameData.timestamp = args.timestamp;
                frameData.width = m_videoCaptureSource->GetWidth();
                frameData.height = m_videoCaptureSource->GetHeight();
                
                m_config.videoFrameCallback(&frameData);
            }
        }
    );
    
    // Start audio capture
    if (!StartAudioCapture(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Start video capture
    if (!m_videoCaptureSource->Start(&hr))
    {
        // If video capture fails, stop audio if it was started
        if (m_audioCaptureSource && m_audioCaptureSource->IsRunning())
        {
            m_audioCaptureSource->Stop();
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
    
    if (!m_audioCaptureSource || !m_videoCaptureSource)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get video dimensions and device
    UINT32 width = m_videoCaptureSource->GetWidth();
    UINT32 height = m_videoCaptureSource->GetHeight();
    ID3D11Device* device = static_cast<WindowsDesktopVideoCaptureSource*>(m_videoCaptureSource.get())->GetDevice();
    
    // Initialize video stream
    // TODO: Use m_config.frameRate, m_config.videoBitrate, and m_config.audioBitrate when implementing encoder settings
    if (!m_sinkWriter->Initialize(m_config.outputPath, device, width, height, &hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize audio stream if audio source is available
    WAVEFORMATEX* audioFormat = m_audioCaptureSource->GetFormat();
    if (audioFormat)
    {
        if (!m_sinkWriter->InitializeAudioStream(audioFormat, &hr))
        {
            if (outHr) *outHr = hr;
            return false;
        }
    }
    
    if (outHr) *outHr = S_OK;
    return true;
}

bool WindowsGraphicsCaptureSession::StartAudioCapture(HRESULT* outHr)
{
    // Only start if audio source exists and has a format (meaning it was initialized)
    if (!m_audioCaptureSource || !m_audioCaptureSource->GetFormat())
    {
        if (outHr) *outHr = S_OK;
        return true;
    }
    
    // Enable/disable audio based on config
    m_audioCaptureSource->SetEnabled(m_config.audioEnabled);
    
    // Start audio capture
    HRESULT hr = S_OK;
    if (!m_audioCaptureSource->Start(&hr))
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

    // Stop video capture first to prevent new frames from arriving
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->Stop();
    }

    // Stop audio capture
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->Stop();
    }

    // Stop the clock after capture sources are stopped
    if (m_mediaClock)
    {
        m_mediaClock->Pause();
    }

    // Allow encoder time to process remaining queued frames (200ms for 6 frames at 30fps)
    Sleep(200);

    // Finalize MP4 file after queue is drained
    m_sinkWriter->Finalize();

    // Reset the clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    m_isActive = false;
}

void WindowsGraphicsCaptureSession::Pause()
{
    if (m_isActive && m_mediaClock)
    {
        m_mediaClock->Pause();
    }
}

void WindowsGraphicsCaptureSession::Resume()
{
    if (m_isActive && m_mediaClock)
    {
        m_mediaClock->Resume();
    }
}

void WindowsGraphicsCaptureSession::ToggleAudioCapture(bool enabled)
{
    // Only toggle if audio capture is currently running
    if (m_audioCaptureSource && m_audioCaptureSource->IsRunning())
    {
        m_audioCaptureSource->SetEnabled(enabled);
    }
}
