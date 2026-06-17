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
#include <psapi.h>
#include <cassert>

namespace {
    /// <summary>
    /// Time to wait for encoder to drain its queue before finalizing.
    /// Allows the encoder to process remaining queued frames.
    /// 200ms is sufficient for approximately 6 frames at 30fps.
    /// </summary>
    constexpr DWORD ENCODER_DRAIN_TIMEOUT_MS = 200;

    void LogWorkingSet(const wchar_t* phase)
    {
        PROCESS_MEMORY_COUNTERS counters{};
        counters.cb = sizeof(counters);
        if (GetProcessMemoryInfo(GetCurrentProcess(), &counters, sizeof(counters)))
        {
            wchar_t message[192]{};
            StringCchPrintfW(
                message,
                ARRAYSIZE(message),
                L"[CaptureInterop V1] Stop memory %ls workingSet=%zu bytes\r\n",
                phase,
                static_cast<size_t>(counters.WorkingSetSize));
            OutputDebugStringW(message);
        }
    }
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
    , m_videoCallbackRegistry()
    , m_audioCallbackRegistry()
    , m_videoCallbackHandle()
    , m_audioCallbackHandle()
{
}

WindowsGraphicsCaptureSession::~WindowsGraphicsCaptureSession()
{
    Stop();
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
    // proper cleanup through RAII.
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

    if (m_audioCaptureSource->Initialize(&hr))
    {
        m_audioAvailable = m_audioCaptureSource->GetFormat() != nullptr;
    }
    else
    {
        OutputDebugStringW(L"[CaptureInterop V1] Audio loopback initialization failed; continuing with video-only timing fallback.\r\n");
        m_audioAvailable = false;
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
    SetupAudioCallback();
    SetupVideoCallback();
}

void WindowsGraphicsCaptureSession::SetupAudioCallback()
{
    // Setup audio callback - writes to sink and forwards to registered callbacks
    if (m_audioAvailable)
    {
        m_audioCaptureSource->SetAudioSampleReadyCallback(
            [this](const AudioSampleReadyEventArgs& args) {
            // Abort if shutting down
            if (m_isShuttingDown.load(std::memory_order_acquire))
            {
                return;
            }

            // Validate audio format before processing
            if (!args.pFormat || args.pFormat->nBlockAlign == 0 || args.pFormat->nSamplesPerSec == 0)
            {
                // Invalid audio format - skip this sample
                return;
            }

            // Write to sink writer
            HRESULT hr = m_sinkWriter->WriteAudioSample(args.data, args.timestamp);
                
            // Disable audio on write failure
            if (FAILED(hr))
            {
                m_audioCaptureSource->SetEnabled(false);
            }

            // Forward to registered callbacks if any exist
            if (args.pFormat && m_audioCallbackRegistry.HasCallbacks())
            {
                AudioSampleData sampleData{};
                sampleData.pData = args.data.data();
                sampleData.numFrames = static_cast<UINT32>(args.data.size()) / args.pFormat->nBlockAlign;
                sampleData.timestamp = args.timestamp;
                sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                sampleData.channels = args.pFormat->nChannels;
                sampleData.bitsPerSample = args.pFormat->wBitsPerSample;
                
                // Invoke all registered callbacks through registry
                m_audioCallbackRegistry.Invoke(sampleData);
            }
            }
        );
    }
}

void WindowsGraphicsCaptureSession::SetupVideoCallback()
{
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
            if (FAILED(hr))
            {
                wchar_t message[192]{};
                StringCchPrintfW(
                    message,
                    ARRAYSIZE(message),
                    L"[CaptureInterop V1] Video frame write failed. HRESULT=0x%08X timestamp=%lld\r\n",
                    static_cast<unsigned int>(hr),
                    args.timestamp);
                OutputDebugStringW(message);
            }

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
    
    if (!m_videoCaptureSource)
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Get video properties
    UINT32 width = m_videoCaptureSource->GetWidth();
    UINT32 height = m_videoCaptureSource->GetHeight();
    ID3D11Device* device = m_videoCaptureSource->GetDevice();
    if (!device)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }
    
    // Initialize video stream
    uint32_t sourceLeft = m_config.targetType == CaptureTargetType::Rectangle
        ? static_cast<uint32_t>(m_config.sourceLeft)
        : 0;
    uint32_t sourceTop = m_config.targetType == CaptureTargetType::Rectangle
        ? static_cast<uint32_t>(m_config.sourceTop)
        : 0;

    if (!m_sinkWriter->Initialize(m_config.outputPath.c_str(), device, width, height, &hr, sourceLeft, sourceTop))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    // Initialize audio stream before writing begins. This keeps the MP4 topology stable
    // for the normal audio+video path; audio initialization failure is handled earlier
    // by leaving m_audioAvailable false.
    WAVEFORMATEX* audioFormat = m_audioAvailable && m_audioCaptureSource ? m_audioCaptureSource->GetFormat() : nullptr;
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
    if (!m_audioAvailable || !m_audioCaptureSource || !m_audioCaptureSource->GetFormat())
    {
        if (outHr) *outHr = S_OK;
        return true;
    }
    
    // Apply audio enabled setting
    m_audioCaptureSource->SetEnabled(m_config.audioEnabled);
    m_audioCaptureSource->SetVolume(m_config.audioInputVolumePercentage);
    
    // Start audio
    HRESULT hr = S_OK;
    if (!m_audioCaptureSource->Start(&hr))
    {
        wchar_t message[256]{};
        StringCchPrintfW(
            message,
            ARRAYSIZE(message),
            L"[CaptureInterop V1] Audio loopback start failed; continuing with video-only timing fallback. HRESULT=0x%08X\r\n",
            static_cast<unsigned int>(hr));
        OutputDebugStringW(message);

        m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
        m_audioAvailable = false;
        m_sinkWriter->Finalize();
        if (!InitializeSinkWriter(&hr))
        {
            if (outHr) *outHr = hr;
            return false;
        }
        SetupVideoCallback();
        if (outHr) *outHr = S_OK;
        return true;
    }
    
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsGraphicsCaptureSession::Stop()
{
    CaptureSessionState state = m_stateMachine.GetState();
    if (m_cleanupCompleted || state == CaptureSessionState::Created || state == CaptureSessionState::Stopped)
    {
        return;
    }

    // Shutdown sequence: set flag, stop sources, clear callbacks, finalize
    m_isShuttingDown.store(true, std::memory_order_release);
    LogWorkingSet(L"begin");

    // Explicitly unregister callback handles before clearing registries so their
    // unregister lambdas do not retain references into the session longer than needed.
    m_videoCallbackHandle.Unregister();
    m_audioCallbackHandle.Unregister();

    // Stop sources
    if (m_videoCaptureSource)
    {
        m_videoCaptureSource->Stop();
    }

    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->Stop();
    }
    LogWorkingSet(L"after-source-stop");

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

    // Finalize sink writer. Active recordings get a short drain; failed/initialized
    // sessions may never have written samples, but Finalize is idempotent.
    if (m_stateMachine.IsActive())
    {
        Sleep(ENCODER_DRAIN_TIMEOUT_MS);
    }
    if (m_sinkWriter)
    {
        m_sinkWriter->Finalize();
    }
    LogWorkingSet(L"after-sink-finalize");

    // Reset clock
    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    // Transition to Stopped state
    m_stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
    m_isShuttingDown.store(false, std::memory_order_release);
    m_cleanupCompleted = true;
    LogWorkingSet(L"end");
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

bool WindowsGraphicsCaptureSession::SetAudioInputSource(const wchar_t* sourceId)
{
    if (!m_audioCaptureSource)
    {
        return false;
    }

    HRESULT hr = S_OK;
    bool wasEnabled = m_audioCaptureSource->IsEnabled();
    if (!m_audioCaptureSource->SetInputDeviceId(sourceId, &hr))
    {
        wchar_t message[256]{};
        StringCchPrintfW(
            message,
            ARRAYSIZE(message),
            L"[CaptureInterop V1] Audio input source switch failed. HRESULT=0x%08X\r\n",
            static_cast<unsigned int>(hr));
        OutputDebugStringW(message);
        return false;
    }

    m_audioAvailable = m_audioCaptureSource->GetFormat() != nullptr;
    m_audioCaptureSource->SetEnabled(wasEnabled);
    return true;
}

void WindowsGraphicsCaptureSession::SetAudioInputVolume(uint32_t volumePercentage)
{
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->SetVolume(volumePercentage);
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
