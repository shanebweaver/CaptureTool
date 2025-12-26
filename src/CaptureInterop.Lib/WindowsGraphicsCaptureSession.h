#pragma once
#include "pch.h"
#include "ICaptureSession.h"
#include "IMP4SinkWriterFactory.h"
#include "CaptureSessionConfig.h"
#include "IMP4SinkWriter.h"
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
/// Windows Graphics Capture API implementation of ICaptureSession.
/// Uses Windows.Graphics.Capture for screen recording with hardware acceleration.
/// Supports both video and audio capture with synchronized media streams.
/// 
/// The session receives fully-initialized dependencies (clock, sources, sink writer)
/// from the factory, ensuring clear ownership semantics and eliminating the need
/// to store factory references.
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

    // Initialization - must be called after construction and before Start()
    // The factory calls this to initialize all sources and sink writer
    // Returns true if initialization succeeded, false otherwise
    bool Initialize(HRESULT* outHr = nullptr);

    // Callback management - can be called at any time, even during recording
    void SetVideoFrameCallback(VideoFrameCallback callback);
    void SetAudioSampleCallback(AudioSampleCallback callback);

private:
    // Helper methods for initialization
    bool InitializeSinkWriter(HRESULT* outHr);
    bool StartAudioCapture(HRESULT* outHr);
    void SetupCallbacks();  // New helper to set up source callbacks

    // Configuration
    CaptureSessionConfig m_config;
    
    // Media output
    std::unique_ptr<IMP4SinkWriter> m_sinkWriter;
    
    // Audio capture
    std::unique_ptr<IAudioCaptureSource> m_audioCaptureSource;
    
    // Video capture
    std::unique_ptr<IVideoCaptureSource> m_videoCaptureSource;
    
    // Media clock for A/V synchronization
    std::unique_ptr<IMediaClock> m_mediaClock;
    
    // Session state
    bool m_isActive;
    bool m_isInitialized;
    std::atomic<bool> m_isShuttingDown{false};
    
    // Callbacks stored as member variables for dynamic updates
    VideoFrameCallback m_videoFrameCallback;
    AudioSampleCallback m_audioSampleCallback;
    
    // Mutex for thread-safe callback access (RAII)
    std::mutex m_callbackMutex;
};
