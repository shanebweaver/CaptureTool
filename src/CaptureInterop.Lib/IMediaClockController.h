#pragma once
#include <cstdint>

/// <summary>
/// Control interface for the MediaClock lifecycle and state management.
/// Allows controllers to manage clock state (start, stop, pause, resume) without advancing time.
/// This interface enforces separation of concerns: lifecycle management vs. time advancement.
/// </summary>
class IMediaClockController
{
public:
    virtual ~IMediaClockController() = default;

    /// <summary>
    /// Start the media clock with an initial timestamp.
    /// Should be called once when recording begins.
    /// </summary>
    /// <param name="startQpc">Starting timestamp in QPC (QueryPerformanceCounter) units.</param>
    virtual void Start(LONGLONG startQpc) = 0;

    /// <summary>
    /// Reset the clock to its initial state.
    /// Called when stopping or restarting recording.
    /// </summary>
    virtual void Reset() = 0;

    /// <summary>
    /// Pause the clock without resetting.
    /// Future feature for supporting pause/resume functionality.
    /// </summary>
    virtual void Pause() = 0;

    /// <summary>
    /// Resume the clock from a paused state.
    /// Future feature for supporting pause/resume functionality.
    /// </summary>
    virtual void Resume() = 0;

    // TODO: New method to Set the audio input source that controls the clock.
};
