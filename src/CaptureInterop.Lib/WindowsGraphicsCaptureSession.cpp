#include "pch.h"
#include "WindowsGraphicsCaptureSession.h"
#include "IAudioCaptureSource.h"
#include "IVideoCaptureSource.h"
#include "IMediaClock.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriter.h"
#include "CallbackTypes.h"
#include "ICaptureSession.h"

#include <mmreg.h>
#include <strsafe.h>
#include <d3d11.h>
#include <Windows.h>
#include <cassert>

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
    , m_videoCallbackRegistry()
    , m_audioCallbackRegistry()
    , m_videoCallbackHandle()
    , m_audioCallbackHandle()
{
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    (void)Stop();
    // Principle #5 (RAII Everything): Destructor ensures all resources are cleaned up
    // automatically via the following chain:
    //
    // 1. Stop() explicitly releases runtime state:
    //    - Stops capture sources (calls source->Stop())
    //    - Clears callbacks
    //    - Finalizes sink writer (flushes buffers and closes file)
    //    - Resets clock state
    //
    // 2. std::unique_ptr destructors automatically clean up owned objects:
    //    - m_mediaClock: No OS resources, just in-memory state
    //    - m_audioCaptureSource: Calls Stop() to release WASAPI handles
    //    - m_videoCaptureSource: Calls Stop() to release Graphics Capture session
    //    - m_sinkWriter: Calls Finalize() to release Media Foundation resources
    //
    // All COM objects use wil::com_ptr for automatic reference counting.
    // No manual delete, free(), or Release() calls needed - the type system guarantees
    // proper cleanup through RAII. See docs/RUST_PRINCIPLES.md principle #5.
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
        // Principle #3 (No Nullable Pointers): This check should never fail if the factory
        // properly initialized all dependencies. After construction, we rely on the type
        // system (std::unique_ptr) to guarantee these are non-null. This check is defensive
        // programming for factory implementation errors.
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
            try
            {
                if (m_isShuttingDown.load(std::memory_order_acquire) ||
                    m_stateMachine.GetState() != CaptureSessionState::Active ||
                    m_hasFailure.load(std::memory_order_acquire))
                {
                    return;
                }

                if (!args.pFormat ||
                    args.pFormat->nBlockAlign == 0 ||
                    args.pFormat->nSamplesPerSec == 0)
                {
                    RecordFailure(E_INVALIDARG, CaptureOperationStage::AudioSampleWrite);
                    return;
                }

                {
                    std::lock_guard<std::mutex> sinkLock(m_sinkMutex);
                    if (m_isShuttingDown.load(std::memory_order_acquire) ||
                        m_stateMachine.GetState() != CaptureSessionState::Active ||
                        m_hasFailure.load(std::memory_order_acquire))
                    {
                        return;
                    }

                    HRESULT hr = m_sinkWriter->WriteAudioSample(args.data, args.timestamp);
                    if (FAILED(hr))
                    {
                        RecordFailure(hr, CaptureOperationStage::AudioSampleWrite);
                        m_audioCaptureSource->SetEnabled(false);
                        return;
                    }
                }

                if (m_stateMachine.GetState() == CaptureSessionState::Active &&
                    m_audioCallbackRegistry.HasCallbacks())
                {
                    AudioSampleData sampleData{};
                    sampleData.pData = args.data.data();
                    sampleData.numFrames = static_cast<UINT32>(args.data.size()) / args.pFormat->nBlockAlign;
                    sampleData.timestamp = args.timestamp;
                    sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                    sampleData.channels = args.pFormat->nChannels;
                    sampleData.bitsPerSample = args.pFormat->wBitsPerSample;

                    m_audioCallbackRegistry.Invoke(sampleData);
                }
            }
            catch (...)
            {
                RecordFailure(E_FAIL, CaptureOperationStage::NativeException);
            }
        }
    );
    
    // Setup video callback - writes to sink and forwards to registered callbacks
    m_videoCaptureSource->SetVideoFrameReadyCallback(
        [this](const VideoFrameReadyEventArgs& args) {
            try
            {
                if (m_isShuttingDown.load(std::memory_order_acquire) ||
                    m_stateMachine.GetState() != CaptureSessionState::Active ||
                    m_hasFailure.load(std::memory_order_acquire))
                {
                    return;
                }

                {
                    std::lock_guard<std::mutex> sinkLock(m_sinkMutex);
                    if (m_isShuttingDown.load(std::memory_order_acquire) ||
                        m_stateMachine.GetState() != CaptureSessionState::Active ||
                        m_hasFailure.load(std::memory_order_acquire))
                    {
                        return;
                    }

                    HRESULT hr = m_sinkWriter->WriteFrame(args.pTexture, args.timestamp);
                    if (FAILED(hr))
                    {
                        RecordFailure(hr, CaptureOperationStage::VideoFrameWrite);
                        return;
                    }
                }

                if (m_stateMachine.GetState() == CaptureSessionState::Active &&
                    m_videoCallbackRegistry.HasCallbacks())
                {
                    VideoFrameData frameData{};
                    frameData.pTexture = args.pTexture;
                    frameData.timestamp = args.timestamp;
                    frameData.width = m_videoCaptureSource->GetWidth();
                    frameData.height = m_videoCaptureSource->GetHeight();

                    m_videoCallbackRegistry.Invoke(frameData);
                }
            }
            catch (...)
            {
                RecordFailure(E_FAIL, CaptureOperationStage::NativeException);
            }
        }
    );
}

void WindowsGraphicsCaptureSession::RecordFailure(
    HRESULT hr,
    CaptureOperationStage stage) noexcept
{
    if (SUCCEEDED(hr))
    {
        return;
    }

    if (m_hasFailure.load(std::memory_order_acquire))
    {
        return;
    }

    std::lock_guard<std::mutex> lock(m_failureMutex);
    if (!m_hasFailure.load(std::memory_order_relaxed))
    {
        m_firstFailure = CaptureOperationResult::Failure(hr, stage);
        m_hasFailure.store(true, std::memory_order_release);
    }
}

CaptureOperationResult WindowsGraphicsCaptureSession::GetRecordedFailure() const noexcept
{
    if (!m_hasFailure.load(std::memory_order_acquire))
    {
        return CaptureOperationResult::Success();
    }

    std::lock_guard<std::mutex> lock(m_failureMutex);
    return m_firstFailure;
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
            (void)m_audioCaptureSource->Stop();
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
    ID3D11Device* device = m_videoCaptureSource->GetDevice();
    
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

CaptureOperationResult WindowsGraphicsCaptureSession::Stop() noexcept
{
    // Check if session is active (Active or Paused state)
    if (!m_stateMachine.IsActive())
    {
        return CaptureOperationResult::Success();
    }

    CaptureOperationResult result = GetRecordedFailure();
    auto retainFirstError = [&result](HRESULT hr, CaptureOperationStage stage)
    {
        if (FAILED(hr) && result.IsSuccess())
        {
            result = CaptureOperationResult::Failure(hr, stage);
        }
    };

    // Shutdown sequence: set flag, stop sources, clear callbacks, finalize
    m_isShuttingDown.store(true, std::memory_order_release);

    // Stop sources
    try
    {
        if (m_videoCaptureSource)
        {
            retainFirstError(m_videoCaptureSource->Stop(), CaptureOperationStage::VideoSourceStop);
        }
    }
    catch (...)
    {
        retainFirstError(E_FAIL, CaptureOperationStage::VideoSourceStop);
    }

    try
    {
        if (m_audioCaptureSource)
        {
            retainFirstError(m_audioCaptureSource->Stop(), CaptureOperationStage::AudioSourceStop);
        }
    }
    catch (...)
    {
        retainFirstError(E_FAIL, CaptureOperationStage::AudioSourceStop);
    }

    if (m_mediaClock)
    {
        m_mediaClock->Pause();
    }

    // Clear callbacks from sources to stop invocations
    try
    {
        if (m_audioCaptureSource)
        {
            m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
        }

        if (m_videoCaptureSource)
        {
            m_videoCaptureSource->SetVideoFrameReadyCallback(nullptr);
        }

        m_videoCallbackRegistry.Clear();
        m_audioCallbackRegistry.Clear();
    }
    catch (...)
    {
        retainFirstError(E_FAIL, CaptureOperationStage::NativeException);
    }

    // Finalize sink writer
    try
    {
        if (m_sinkWriter)
        {
            std::lock_guard<std::mutex> sinkLock(m_sinkMutex);
            retainFirstError(m_sinkWriter->Finalize(), CaptureOperationStage::SinkFinalize);
        }
    }
    catch (...)
    {
        retainFirstError(E_FAIL, CaptureOperationStage::SinkFinalize);
    }

    // Reset clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    // Transition to Stopped state
    m_stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
    m_isShuttingDown.store(false, std::memory_order_release);
    return result;
}

void WindowsGraphicsCaptureSession::Pause()
{
    // Can only pause from Active state
    if (m_stateMachine.GetState() == CaptureSessionState::Active && m_mediaClock)
    {
        std::lock_guard<std::mutex> sinkLock(m_sinkMutex);
        if (m_stateMachine.GetState() != CaptureSessionState::Active)
        {
            return;
        }

        m_mediaClock->Pause();
        m_stateMachine.TryTransitionTo(CaptureSessionState::Paused);
    }
}

void WindowsGraphicsCaptureSession::Resume()
{
    // Can only resume from Paused state
    if (m_stateMachine.GetState() == CaptureSessionState::Paused && m_mediaClock)
    {
        std::lock_guard<std::mutex> sinkLock(m_sinkMutex);
        if (m_stateMachine.GetState() != CaptureSessionState::Paused)
        {
            return;
        }

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
    try
    {
        // Unregister existing callback if any
        if (m_videoCallbackHandle.IsValid())
        {
            m_videoCallbackHandle.Unregister();
        }
        
        // Register new callback if provided and STORE THE HANDLE
        if (callback)
        {
            m_videoCallbackHandle = m_videoCallbackRegistry.Register([callback](const VideoFrameData& data) {
                if (callback)  // Extra safety check
                {
                    callback(const_cast<VideoFrameData*>(&data));
                }
            });
        }
    }
    catch (...)
    {
        // Swallow any exceptions to prevent crash
        // Callback registration failure is not fatal
    }
}

void WindowsGraphicsCaptureSession::SetAudioSampleCallback(AudioSampleCallback callback)
{
    try
    {
        // Unregister existing callback if any
        if (m_audioCallbackHandle.IsValid())
        {
            m_audioCallbackHandle.Unregister();
        }
        
        // Register new callback if provided and STORE THE HANDLE
        if (callback)
        {
            m_audioCallbackHandle = m_audioCallbackRegistry.Register([callback](const AudioSampleData& data) {
                if (callback)  // Extra safety check
                {
                    callback(const_cast<AudioSampleData*>(&data));
                }
            });
        }
    }
    catch (...)
    {
        // Swallow any exceptions to prevent crash
        // Callback registration failure is not fatal
    }
}
