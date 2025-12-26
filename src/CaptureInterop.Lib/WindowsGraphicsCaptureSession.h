#pragma once
#include "pch.h"
#include "ICaptureSession.h"
#include "IMP4SinkWriterFactory.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriter.h"
#include "CallbackRegistry.h"
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
    bool IsActive() const override { return m_isActive; }

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
    
    // State
    bool m_isActive;
    bool m_isInitialized;
    std::atomic<bool> m_isShuttingDown{false};
    
    // Callbacks - using registry for safer lifetime management
    CaptureInterop::CallbackRegistry<VideoFrameData> m_videoCallbackRegistry;
    CaptureInterop::CallbackRegistry<AudioSampleData> m_audioCallbackRegistry;
    
    // Legacy callback support for P/Invoke compatibility
    VideoFrameCallback m_legacyVideoCallback;
    AudioSampleCallback m_legacyAudioCallback;
    std::mutex m_legacyCallbackMutex;
};
