#pragma once
#include <cstdint>

/// <summary>
/// Read-only interface for the MediaClock.
/// Provides access to current media time and playback information.
/// This interface enforces separation of concerns: readers cannot advance the clock.
/// </summary>
class IMediaClockReader
{
public:
    virtual ~IMediaClockReader() = default;

    /// <summary>
    /// Get the current media time in 100-nanosecond units (REFERENCE_TIME).
    /// This is the synchronized timeline position for all media streams.
    /// </summary>
    /// <returns>Current media time in 100ns ticks.</returns>
    virtual LONGLONG GetCurrentTime() const = 0;

    /// <summary>
    /// Get the start time of the recording session in QPC (QueryPerformanceCounter) units.
    /// This represents the absolute system time when recording began.
    /// </summary>
    /// <returns>Recording start time as QPC value.</returns>
    virtual LONGLONG GetStartTime() const = 0;

    /// <summary>
    /// Calculate relative time from recording start to a given QPC timestamp.
    /// Useful for converting absolute system timestamps to media timeline positions.
    /// </summary>
    /// <param name="qpcTimestamp">QPC timestamp to convert.</param>
    /// <returns>Relative time in 100ns ticks since recording started.</returns>
    virtual LONGLONG GetRelativeTime(LONGLONG qpcTimestamp) const = 0;

    /// <summary>
    /// Check if the media clock has been started.
    /// </summary>
    /// <returns>True if clock is running, false otherwise.</returns>
    virtual bool IsRunning() const = 0;

    /// <summary>
    /// Get the frequency of the QueryPerformanceCounter in ticks per second.
    /// Used for converting between QPC units and time units.
    /// </summary>
    /// <returns>QPC frequency.</returns>
    virtual LONGLONG GetQpcFrequency() const = 0;
};
