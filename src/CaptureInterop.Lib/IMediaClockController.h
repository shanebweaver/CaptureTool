#pragma once
#include <cstdint>

// Forward declaration
class IMediaClockAdvancer;

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

    /// <summary>
    /// Set the component that will drive clock advancement.
    /// The clock provides its IMediaClockWriter interface to the advancer,
    /// allowing the advancer to update the timeline as media is processed.
    /// This establishes the master timing source for accurate A/V synchronization.
    /// Typically, audio is used as the clock advancer for sample-accurate timing.
    /// </summary>
    /// <param name="advancer">Component that will advance the clock timeline.</param>
    virtual void SetClockAdvancer(IMediaClockAdvancer* advancer) = 0;
};
