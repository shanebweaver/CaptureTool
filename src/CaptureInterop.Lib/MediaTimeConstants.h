#pragma once
#include <cstdint>

/// <summary>
/// Media time conversion constants and utility functions.
/// All time values are in 100-nanosecond units (REFERENCE_TIME).
/// Using constexpr functions allows compile-time computation while providing clear semantics.
/// </summary>
namespace MediaTimeConstants
{
    /// <summary>
    /// Number of 100-nanosecond ticks in one second.
    /// </summary>
    constexpr int64_t TicksPerSecond() { return 10'000'000; }

    /// <summary>
    /// Number of 100-nanosecond ticks in one millisecond.
    /// </summary>
    constexpr int64_t TicksPerMillisecond() { return TicksPerSecond() / 1000; }

    /// <summary>
    /// Convert milliseconds to 100-nanosecond ticks.
    /// </summary>
    constexpr int64_t TicksFromMilliseconds(int64_t ms) 
    { 
        return ms * TicksPerMillisecond(); 
    }

    /// <summary>
    /// Convert seconds to 100-nanosecond ticks.
    /// </summary>
    constexpr int64_t TicksFromSeconds(int64_t seconds) 
    { 
        return seconds * TicksPerSecond(); 
    }

    /// <summary>
    /// Convert ticks to milliseconds.
    /// </summary>
    constexpr int64_t MillisecondsFromTicks(int64_t ticks) 
    { 
        return ticks / TicksPerMillisecond(); 
    }

    /// <summary>
    /// Calculate duration in ticks for a given number of audio frames at a sample rate.
    /// Precondition: sampleRate must be greater than zero.
    /// </summary>
    constexpr int64_t TicksFromAudioFrames(uint32_t numFrames, uint32_t sampleRate) 
    { 
        return (static_cast<int64_t>(numFrames) * TicksPerSecond()) / sampleRate; 
    }
}
