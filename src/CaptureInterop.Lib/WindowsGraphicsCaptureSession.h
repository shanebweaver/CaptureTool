#pragma once
#include "pch.h"
#include "ICaptureSession.h"
#include "IMP4SinkWriterFactory.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriter.h"
#include "CallbackRegistry.h"
#include "CallbackHandle.h"
#include "CaptureSessionState.h"
#include <Windows.h>
#include <memory>
#include <atomic>
#include <mutex>

// Forward declarations
class FrameArrivedHandler;
class IAudioCaptureSource;
class IVideoCaptureSource;
class IMediaClock;

// Callback data structures
struct VideoFrameData;
struct AudioSampleData;
using VideoFrameCallback = void(__stdcall*)(const VideoFrameData* pFrameData);
using AudioSampleCallback = void(__stdcall*)(const AudioSampleData* pSampleData);

/// <summary>
/// Windows Graphics Capture API implementation for screen recording with hardware acceleration.
/// Dependencies are injected via constructor for clear ownership semantics.
/// 
/// Implements Rust Principles:
/// - Principle #3 (No Nullable Pointers): Uses std::unique_ptr for all dependencies,
///   ensuring resources are always valid after construction. No null checks needed
///   in most methods because ownership guarantees validity.
/// - Principle #5 (RAII Everything): Destructor automatically cleans up all resources
///   (calls Stop() to release capture devices, buffers, and file handles).
/// - Principle #6 (No Globals): All dependencies injected through constructor,
///   no global session state. Each instance is independent.
/// - Principle #9 (State Machine in Types): Uses CaptureSessionStateMachine to
///   enforce valid state transitions and prevent misuse.
/// 
/// See docs/RUST_PRINCIPLES.md for more details on these principles.
/// </summary>
class WindowsGraphicsCaptureSession : public ICaptureSession
{
public:
    WindowsGraphicsCaptureSession(
        const CaptureSessionConfig& config,
        std::unique_ptr<IMediaClock> mediaClock,
        std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
        std::unique_ptr<IVideoCaptureSource> videoCaptureSource,
        std::unique_ptr<IMP4SinkWriter> sinkWriter);
    ~WindowsGraphicsCaptureSession() override;

    // Delete copy and move operations
    WindowsGraphicsCaptureSession(const WindowsGraphicsCaptureSession&) = delete;
    WindowsGraphicsCaptureSession& operator=(const WindowsGraphicsCaptureSession&) = delete;
    WindowsGraphicsCaptureSession(WindowsGraphicsCaptureSession&&) = delete;
    WindowsGraphicsCaptureSession& operator=(WindowsGraphicsCaptureSession&&) = delete;

    // ICaptureSession implementation
    bool Start(HRESULT* outHr = nullptr) override;
    void Stop() override;
    void Pause() override;
    void Resume() override;
    void ToggleAudioCapture(bool enabled) override;
    bool IsActive() const override { return m_stateMachine.IsActive(); }

    // Initialize sources and sink writer (called by factory after construction)
    bool Initialize(HRESULT* outHr = nullptr);

    /// <summary>
    /// Set callback for video frame notifications.
    /// Thread-safe. Callback will not be invoked after Stop() returns.
    /// </summary>
    void SetVideoFrameCallback(VideoFrameCallback callback);

    /// <summary>
    /// Set callback for audio sample notifications.
    /// Thread-safe. Callback will not be invoked after Stop() returns.
    /// </summary>
    void SetAudioSampleCallback(AudioSampleCallback callback);

private:
    // Helper methods
    bool InitializeSinkWriter(HRESULT* outHr);
    bool StartAudioCapture(HRESULT* outHr);
    void SetupCallbacks();

    // Configuration and dependencies
    CaptureSessionConfig m_config;
    std::unique_ptr<IMP4SinkWriter> m_sinkWriter;
    std::unique_ptr<IAudioCaptureSource> m_audioCaptureSource;
    std::unique_ptr<IVideoCaptureSource> m_videoCaptureSource;
    std::unique_ptr<IMediaClock> m_mediaClock;
    
    // State management
    CaptureSessionStateMachine m_stateMachine;
    std::atomic<bool> m_isShuttingDown{false}; // For thread coordination during shutdown
    
    // Callbacks - using registry for safer lifetime management
    CaptureInterop::CallbackRegistry<VideoFrameData> m_videoCallbackRegistry;
    CaptureInterop::CallbackRegistry<AudioSampleData> m_audioCallbackRegistry;
    
    // Callback handles - must be stored to keep callbacks registered!
    CaptureInterop::CallbackHandle m_videoCallbackHandle;
    CaptureInterop::CallbackHandle m_audioCallbackHandle;
};
