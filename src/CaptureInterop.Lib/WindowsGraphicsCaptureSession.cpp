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
#include "CallbackTypes.h"
#include "ICaptureSession.h"

#include <mmreg.h>
#include <strsafe.h>
#include <d3d11.h>
#include <Windows.h>

namespace {
    /// <summary>
    /// Time to wait for encoder to drain its queue before finalizing.
    /// Allows the encoder to process remaining queued frames.
    /// 200ms is sufficient for approximately 6 frames at 30fps.
    /// </summary>
    constexpr DWORD ENCODER_DRAIN_TIMEOUT_MS = 200;
}

WindowsGraphicsCaptureSession::WindowsGraphicsCaptureSession(
    const CaptureSessionConfig& config,
    std::unique_ptr<IMediaClock> mediaClock,
    std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
    std::unique_ptr<IVideoCaptureSource> videoCaptureSource,
    std::unique_ptr<IMP4SinkWriter> sinkWriter)
    : m_config(config)
    , m_mediaClock(std::move(mediaClock))
    , m_audioCaptureSource(std::move(audioCaptureSource))
    , m_videoCaptureSource(std::move(videoCaptureSource))
    , m_sinkWriter(std::move(sinkWriter))
    , m_isActive(false)
    , m_isInitialized(false)
    , m_videoFrameCallback(nullptr)
    , m_audioSampleCallback(nullptr)
{
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
}

bool WindowsGraphicsCaptureSession::Initialize(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Validate dependencies
    if (!m_mediaClock || !m_audioCaptureSource || !m_videoCaptureSource || !m_sinkWriter)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Prevent double initialization
    if (m_isInitialized)
    {
        if (outHr) *outHr = S_OK;
        return true;
    }

    // Connect audio source as clock advancer
    m_mediaClock->SetClockAdvancer(m_audioCaptureSource.get());

    // Initialize sources
    if (!m_videoCaptureSource->Initialize(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    if (!m_audioCaptureSource->Initialize(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize sink writer
    if (!InitializeSinkWriter(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Setup callbacks
    SetupCallbacks();

    m_isInitialized = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::SetupCallbacks()
{
    // Setup audio callback - writes to sink and forwards to managed layer
    m_audioCaptureSource->SetAudioSampleReadyCallback(
        [this](const AudioSampleReadyEventArgs& args) {
            // Abort if shutting down
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;
            }

            // Write to sink writer
            HRESULT hr = m_sinkWriter->WriteAudioSample(args.pData, args.numFrames, args.timestamp);
                
            // Disable audio on write failure
            if (FAILED(hr))
            {
                m_audioCaptureSource->SetEnabled(false);
            }

            // Forward to managed layer
            AudioSampleCallback callback;
            {
                std::lock_guard<std::mutex> lock(m_callbackMutex);
                callback = m_audioSampleCallback;
            }
            
            if (callback && args.pFormat)
            {
                AudioSampleData sampleData{};
                sampleData.pData = args.pData;
                sampleData.numFrames = args.numFrames;
                sampleData.timestamp = args.timestamp;
                sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                sampleData.channels = args.pFormat->nChannels;
                sampleData.bitsPerSample = args.pFormat->wBitsPerSample;
                
                callback(&sampleData);
            }
        }
    );
    
    // Setup video callback - writes to sink and forwards to managed layer
    m_videoCaptureSource->SetVideoFrameReadyCallback(
        [this](const VideoFrameReadyEventArgs& args) {
            // Abort if shutting down
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;
            }

            // Write to sink writer
            HRESULT hr = m_sinkWriter->WriteFrame(args.pTexture, args.timestamp);

            // Forward to managed layer
            VideoFrameCallback callback;
            {
                std::lock_guard<std::mutex> lock(m_callbackMutex);
                callback = m_videoFrameCallback;
            }
            
            if (callback)
            {
                VideoFrameData frameData{};
                frameData.pTexture = args.pTexture;
                frameData.timestamp = args.timestamp;
                frameData.width = m_videoCaptureSource->GetWidth();
                frameData.height = m_videoCaptureSource->GetHeight();
                
                callback(&frameData);
            }
        }
    );
}

bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Validate state
    if (!m_isInitialized)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    if (m_isActive)
    {
        if (outHr) *outHr = S_OK;
        return true;
    }

    // Start clock
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_mediaClock->Start(qpc.QuadPart);

    // Start audio capture
    if (!StartAudioCapture(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Start video capture
    if (!m_videoCaptureSource->Start(&hr))
    {
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

    // Get video properties
    UINT32 width = m_videoCaptureSource->GetWidth();
    UINT32 height = m_videoCaptureSource->GetHeight();
    ID3D11Device* device = static_cast<WindowsDesktopVideoCaptureSource*>(m_videoCaptureSource.get())->GetDevice();
    
    // Initialize video stream
    if (!m_sinkWriter->Initialize(m_config.outputPath.c_str(), device, width, height, &hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }
    
    // Initialize audio stream
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
    // Check if audio is available
    if (!m_audioCaptureSource || !m_audioCaptureSource->GetFormat())
    {
        if (outHr) *outHr = S_OK;
        return true;
    }
    
    // Apply audio enabled setting
    m_audioCaptureSource->SetEnabled(m_config.audioEnabled);
    
    // Start audio
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

    // Shutdown sequence: set flag, stop sources, clear callbacks, finalize
    m_isShuttingDown.store(true, std::memory_order_release);

    // Stop sources
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->Stop();
    }

    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->Stop();
    }

    if (m_mediaClock)
    {
        m_mediaClock->Pause();
    }

    // Clear callbacks
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
    }
    
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->SetVideoFrameReadyCallback(nullptr);
    }

    // Finalize sink writer
    Sleep(ENCODER_DRAIN_TIMEOUT_MS);
    m_sinkWriter->Finalize();

    // Reset clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    m_isActive = false;
    m_isShuttingDown.store(false, std::memory_order_release);
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
    if (m_audioCaptureSource && m_audioCaptureSource->IsRunning())
    {
        m_audioCaptureSource->SetEnabled(enabled);
    }
}

void WindowsGraphicsCaptureSession::SetVideoFrameCallback(VideoFrameCallback callback)
{
    std::lock_guard<std::mutex> lock(m_callbackMutex);
    m_videoFrameCallback = callback;
}

void WindowsGraphicsCaptureSession::SetAudioSampleCallback(AudioSampleCallback callback)
{
    std::lock_guard<std::mutex> lock(m_callbackMutex);
    m_audioSampleCallback = callback;
}
