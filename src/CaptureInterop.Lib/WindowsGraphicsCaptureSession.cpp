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
    // std::mutex constructor handles initialization (RAII)
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
    // std::mutex destructor handles cleanup (RAII)
}

bool WindowsGraphicsCaptureSession::Initialize(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Guard: Validate that all required dependencies were provided
    if (!m_mediaClock || !m_audioCaptureSource || !m_videoCaptureSource || !m_sinkWriter)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Guard: Prevent double initialization
    if (m_isInitialized)
    {
        if (outHr) *outHr = S_OK;
        return true;
    }

    // Connect the audio source as the clock advancer so it drives the timeline
    m_mediaClock->SetClockAdvancer(m_audioCaptureSource.get());

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
    if (!InitializeSinkWriter(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Set up callbacks for audio and video sources
    SetupCallbacks();

    m_isInitialized = true;
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::SetupCallbacks()
{
    // Set up audio sample callback to write to sink writer and forward to managed layer
    // 
    // Lifetime Safety:
    // - Lambda captures 'this' by value
    // - 'm_isShuttingDown' flag ensures callbacks abort during shutdown
    // - Stop() sequence: (1) set shutdown flag, (2) stop sources, (3) clear callbacks
    // - Sources guarantee no callbacks after Stop() returns
    // - Therefore, 'this' is always valid when callback executes
    m_audioCaptureSource->SetAudioSampleReadyCallback(
        [this](const AudioSampleReadyEventArgs& args) {
            // Check if shutting down (acquire memory order ensures visibility of Stop() changes)
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;  // Abort callback invocation - session is shutting down
            }

            // Write audio sample to sink writer
            HRESULT hr = m_sinkWriter->WriteAudioSample(args.pData, args.numFrames, args.timestamp);
                
            // If write fails, disable audio to prevent further blocking
            if (FAILED(hr))
            {
                m_audioCaptureSource->SetEnabled(false);
            }

            // Forward to managed layer if callback is set
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
    
    // Set up video frame callback to write to sink writer and forward to managed layer
    // 
    // Lifetime Safety:
    // - Lambda captures 'this' by value
    // - 'm_isShuttingDown' flag ensures callbacks abort during shutdown
    // - Stop() sequence: (1) set shutdown flag, (2) stop sources, (3) clear callbacks
    // - Sources guarantee no callbacks after Stop() returns
    // - Therefore, 'this' is always valid when callback executes
    m_videoCaptureSource->SetVideoFrameReadyCallback(
        [this](const VideoFrameReadyEventArgs& args) {
            // Check if shutting down (acquire memory order ensures visibility of Stop() changes)
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;  // Abort callback invocation - session is shutting down
            }

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

    // Guard: Session must be initialized before starting
    if (!m_isInitialized)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Guard: Prevent starting if already active
    if (m_isActive)
    {
        if (outHr) *outHr = S_OK;
        return true;
    }

    // Start the media clock
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    LONGLONG startQpc = qpc.QuadPart;
    m_mediaClock->Start(startQpc);

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
    if (!m_sinkWriter->Initialize(m_config.outputPath.c_str(), device, width, height, &hr))
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

    // Shutdown Sequence (carefully ordered to ensure thread safety):
    // 
    // 1. Set shutdown flag FIRST (atomic operation with release semantics)
    //    - All subsequent callback invocations will see this flag and abort immediately
    //    - Memory order release ensures all writes before this are visible to other threads
    //
    // 2. Stop capture sources
    //    - Sources stop generating new callbacks
    //    - Wait for in-flight callbacks to complete
    //
    // 3. Clear callbacks
    //    - Safe to clear because sources guarantee no more callbacks after Stop() returns
    //
    // 4. Finalize resources
    //    - Flush encoder and finalize output file
    //
    // This sequence ensures:
    // - No use-after-free: callbacks see shutdown flag before accessing member variables
    // - No dangling callbacks: sources stopped before callbacks cleared
    // - Thread safety: atomic flag with proper memory ordering
    
    // Step 1: Set shutdown flag (atomic store with release memory order)
    m_isShuttingDown.store(true, std::memory_order_release);

    // Step 2: Stop capture sources (they wait for in-flight callbacks to complete)
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

    // Step 3: Clear callbacks - safe now because sources have stopped and no more callbacks will fire
    // NOW it's safe to clear callbacks - no threads can invoke them
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
    }
    
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->SetVideoFrameReadyCallback(nullptr);
    }

    // Step 4: Finalize resources
    // Allow encoder time to process remaining queued frames
    Sleep(ENCODER_DRAIN_TIMEOUT_MS);

    // Finalize MP4 file after queue is drained
    m_sinkWriter->Finalize();

    // Reset the clock
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
    // Only toggle if audio capture is currently running
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
}
