#pragma once
#include <Windows.h>

/// <summary>
/// Interface for a capture session that manages audio and video recording.
/// Provides operations for controlling the lifecycle of a recording session.
/// Implementations should handle platform-specific capture mechanisms.
/// Configuration is provided via constructor when the session is created.
/// </summary>
class ICaptureSession
{
public:
    virtual ~ICaptureSession() = default;

    /// <summary>
    /// Initialize and start the capture session with the configuration provided at construction.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if session started successfully, false otherwise.</returns>
    virtual bool Start(HRESULT* outHr = nullptr) = 0;

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
