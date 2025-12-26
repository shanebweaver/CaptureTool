#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "IAudioCaptureSource.h"
#include "IVideoCaptureSource.h"
#include "IMediaClock.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriter.h"
#include "WindowsDesktopVideoCaptureSource.h"
#include "CallbackTypes.h"
#include "ICaptureSession.h"

#include <mmreg.h>
#include <strsafe.h>
#include <d3d11.h>
#include <Windows.h>
#include <cassert>

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
    , m_stateMachine() // Initializes in Created state
{
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
}

bool WindowsGraphicsCaptureSession::Initialize(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Validate state - must be in Created state to initialize
    if (m_stateMachine.GetState() != CaptureSessionState::Created)
    {
        // Already initialized or in invalid state
        if (m_stateMachine.GetState() == CaptureSessionState::Initialized)
        {
            if (outHr) *outHr = S_OK;
            return true;
        }
        if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
        return false;
    }

    // Validate dependencies
    if (!m_mediaClock || !m_audioCaptureSource || !m_videoCaptureSource || !m_sinkWriter)
    {
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        // Transition should always succeed from Created to Failed
        assert(transitioned && "Transition to Failed should always succeed from Created state");
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    // Connect audio source as clock advancer
    m_mediaClock->SetClockAdvancer(m_audioCaptureSource.get());

    // Initialize sources
    if (!m_videoCaptureSource->Initialize(&hr))
    {
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        assert(transitioned && "Transition to Failed should always succeed from Created state");
        if (outHr) *outHr = hr;
        return false;
    }

    if (!m_audioCaptureSource->Initialize(&hr))
    {
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        assert(transitioned && "Transition to Failed should always succeed from Created state");
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize sink writer
    if (!InitializeSinkWriter(&hr))
    {
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        assert(transitioned && "Transition to Failed should always succeed from Created state");
        if (outHr) *outHr = hr;
        return false;
    }

    // Setup callbacks
    SetupCallbacks();

    // Transition to Initialized state on success
    m_stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::SetupCallbacks()
{
    // Setup audio callback - writes to sink and forwards to registered callbacks
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

            // Forward to registered callbacks if any exist
            if (args.pFormat && m_audioCallbackRegistry.HasCallbacks())
            {
                AudioSampleData sampleData{};
                sampleData.pData = args.pData;
                sampleData.numFrames = args.numFrames;
                sampleData.timestamp = args.timestamp;
                sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                sampleData.channels = args.pFormat->nChannels;
                sampleData.bitsPerSample = args.pFormat->wBitsPerSample;
                
                // Invoke all registered callbacks through registry
                m_audioCallbackRegistry.Invoke(sampleData);
            }
        }
    );
    
    // Setup video callback - writes to sink and forwards to registered callbacks
    m_videoCaptureSource->SetVideoFrameReadyCallback(
        [this](const VideoFrameReadyEventArgs& args) {
            // Abort if shutting down
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;
            }

            // Write to sink writer
            HRESULT hr = m_sinkWriter->WriteFrame(args.pTexture, args.timestamp);

            // Forward to registered callbacks if any exist
            if (m_videoCallbackRegistry.HasCallbacks())
            {
                VideoFrameData frameData{};
                frameData.pTexture = args.pTexture;
                frameData.timestamp = args.timestamp;
                frameData.width = m_videoCaptureSource->GetWidth();
                frameData.height = m_videoCaptureSource->GetHeight();
                
                // Invoke all registered callbacks through registry
                m_videoCallbackRegistry.Invoke(frameData);
            }
        }
    );
}

bool WindowsGraphicsCaptureSession::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    // Validate state - can only start from Initialized state
    if (!m_stateMachine.CanTransitionTo(CaptureSessionState::Active))
    {
        // If already active, return success
        if (m_stateMachine.GetState() == CaptureSessionState::Active)
        {
            if (outHr) *outHr = S_OK;
            return true;
        }
        if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
        return false;
    }

    // Start clock
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_mediaClock->Start(qpc.QuadPart);

    // Start audio capture
    if (!StartAudioCapture(&hr))
    {
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        assert(transitioned && "Transition to Failed should always succeed from Initialized state");
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
        [[maybe_unused]] bool transitioned = m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        assert(transitioned && "Transition to Failed should always succeed from Initialized state");
        if (outHr) *outHr = hr;
        return false;
    }

    // Transition to Active state on success
    m_stateMachine.TryTransitionTo(CaptureSessionState::Active);
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
    // Check if session is active (Active or Paused state)
    if (!m_stateMachine.IsActive())
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

    // Clear callbacks from sources to stop invocations
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
    }
    
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->SetVideoFrameReadyCallback(nullptr);
    }
    
    // Clear all registered callbacks through registry
    // Note: The m_isShuttingDown flag ensures no callbacks are invoked after Stop() is called.
    // Sources are stopped first, then callbacks are cleared, ensuring synchronization.
    m_videoCallbackRegistry.Clear();
    m_audioCallbackRegistry.Clear();

    // Finalize sink writer
    Sleep(ENCODER_DRAIN_TIMEOUT_MS);
    m_sinkWriter->Finalize();

    // Reset clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    // Transition to Stopped state
    m_stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
    m_isShuttingDown.store(false, std::memory_order_release);
}

void WindowsGraphicsCaptureSession::Pause()
{
    // Can only pause from Active state
    if (m_stateMachine.GetState() == CaptureSessionState::Active && m_mediaClock)
    {
        m_mediaClock->Pause();
        m_stateMachine.TryTransitionTo(CaptureSessionState::Paused);
    }
}

void WindowsGraphicsCaptureSession::Resume()
{
    // Can only resume from Paused state
    if (m_stateMachine.GetState() == CaptureSessionState::Paused && m_mediaClock)
    {
        m_mediaClock->Resume();
        m_stateMachine.TryTransitionTo(CaptureSessionState::Active);
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
    // Clear existing P/Invoke callbacks
    m_videoCallbackRegistry.Clear();
    
    // Register new callback if provided
    if (callback)
    {
        // Store handle to keep callback registered
        // Note: Using a static ID since P/Invoke expects only one callback at a time
        m_videoCallbackRegistry.Register([callback](const VideoFrameData& data) {
            callback(const_cast<VideoFrameData*>(&data));
        });
    }
}

void WindowsGraphicsCaptureSession::SetAudioSampleCallback(AudioSampleCallback callback)
{
    // Clear existing P/Invoke callbacks
    m_audioCallbackRegistry.Clear();
    
    // Register new callback if provided
    if (callback)
    {
        // Store handle to keep callback registered
        // Note: Using a static ID since P/Invoke expects only one callback at a time
        m_audioCallbackRegistry.Register([callback](const AudioSampleData& data) {
            callback(const_cast<AudioSampleData*>(&data));
        });
    }
}
