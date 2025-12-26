#pragma once
#include "pch.h"
#include "ICaptureSession.h"
#include "ICaptureSessionFactory.h"
#include "CaptureSessionConfig.h"

/// <summary>
/// Implementation class for screen recording functionality.
/// Manages the capture session lifecycle and callbacks to managed layer.
/// 
/// Implements Rust Principles:
/// - Principle #3 (No Nullable Pointers): Uses std::unique_ptr for session ownership.
///   The session pointer is nullable by design (session only exists when recording),
///   but we provide HasActiveSession() to make checks explicit rather than testing
///   raw pointers.
/// - Principle #5 (RAII Everything): Destructor calls StopRecording() to ensure
///   proper cleanup even if caller forgets.
/// - Principle #6 (No Globals): Session factory injected via constructor, no global
///   recorder instance.
/// 
/// Ownership model:
/// - ScreenRecorderImpl owns the ICaptureSessionFactory (lifetime of recorder)
/// - ScreenRecorderImpl owns the ICaptureSession (lifetime of current recording)
/// - Session is created in StartRecording() and destroyed in StopRecording()
/// 
/// See docs/RUST_PRINCIPLES.md for more details.
/// </summary>
class ScreenRecorderImpl
{
public:
    /// <summary>
    /// Constructor that takes a factory for creating capture sessions.
    /// </summary>
    /// <param name="factory">Factory for creating ICaptureSession instances. Ownership is transferred.</param>
    explicit ScreenRecorderImpl(std::unique_ptr<ICaptureSessionFactory> factory);
    
    /// <summary>
    /// Default constructor that creates a default factory.
    /// </summary>
    ScreenRecorderImpl();
    
    ~ScreenRecorderImpl();

    /// <summary>
    /// Start recording with configuration settings.
    /// </summary>
    /// <param name="config">Configuration settings for the capture session.</param>
    /// <returns>True if recording started successfully, false otherwise.</returns>
    bool StartRecording(const CaptureSessionConfig& config);

    /// <summary>
    /// Start recording the specified monitor to an output file.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="captureAudio">Whether to capture system audio.</param>
    /// <returns>True if recording started successfully, false otherwise.</returns>
    bool StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio);

    /// <summary>
    /// Pause the current recording.
    /// </summary>
    void PauseRecording();

    /// <summary>
    /// Resume the paused recording.
    /// </summary>
    void ResumeRecording();

    /// <summary>
    /// Stop the current recording and finalize the output file.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Toggle audio capture on/off during recording.
    /// </summary>
    /// <param name="enabled">True to enable audio, false to mute.</param>
    void ToggleAudioCapture(bool enabled);

    /// <summary>
    /// Set the callback to be invoked when a video frame is ready.
    /// </summary>
    /// <param name="callback">Callback function to receive video frames.</param>
    void SetVideoFrameCallback(VideoFrameCallback callback);

    /// <summary>
    /// Set the callback to be invoked when an audio sample is ready.
    /// </summary>
    /// <param name="callback">Callback function to receive audio samples.</param>
    void SetAudioSampleCallback(AudioSampleCallback callback);

    /// <summary>
    /// Check if there is an active recording session.
    /// Provides explicit alternative to checking m_captureSession pointer.
    /// Implements Principle #3: Make nullable states explicit.
    /// </summary>
    /// <returns>True if a session exists, false otherwise.</returns>
    bool HasActiveSession() const { return m_captureSession != nullptr; }

private:
    std::unique_ptr<ICaptureSession> m_captureSession;
    std::unique_ptr<ICaptureSessionFactory> m_factory;
};
