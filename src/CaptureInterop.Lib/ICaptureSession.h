#pragma once
#include <cstdint>

/// <summary>
/// Interface for a capture session that manages audio and video recording.
/// Provides operations for controlling the lifecycle of a recording session.
/// Implementations should handle platform-specific capture mechanisms.
/// </summary>
class ICaptureSession
{
public:
    virtual ~ICaptureSession() = default;

    /// <summary>
    /// Initialize and start the capture session.
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor to capture.</param>
    /// <param name="outputPath">Path to the output MP4 file.</param>
    /// <param name="captureAudio">Whether to capture system audio.</param>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if session started successfully, false otherwise.</returns>
    virtual bool Start(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio, HRESULT* outHr = nullptr) = 0;

    /// <summary>
    /// Stop the capture session and finalize the output file.
    /// Ensures all resources are properly released and file is written.
    /// </summary>
    virtual void Stop() = 0;

    /// <summary>
    /// Toggle audio capture on/off during recording.
    /// Allows muting/unmuting without stopping the entire session.
    /// </summary>
    /// <param name="enabled">True to enable audio, false to mute.</param>
    virtual void ToggleAudioCapture(bool enabled) = 0;

    /// <summary>
    /// Check if the session is currently active.
    /// </summary>
    /// <returns>True if session is running, false otherwise.</returns>
    virtual bool IsActive() const = 0;
};
