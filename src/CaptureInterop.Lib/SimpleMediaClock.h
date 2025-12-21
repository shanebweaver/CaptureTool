#pragma once
#include "IMediaClock.h"
#include <atomic>
#include <mutex>

/// <summary>
/// SimpleMediaClock provides a unified timeline for synchronizing audio and video streams.
/// 
/// Design principles:
/// - Implements IMediaClock interface with three responsibilities:
///   * IMediaClockReader (read-only access)
///   * IMediaClockController (lifecycle management)
///   * IMediaClockWriter (time advancement)
/// - Uses audio samples as the authoritative time source for accurate A/V sync
/// - Thread-safe for concurrent access from audio and video capture threads
/// - Converts between QPC (QueryPerformanceCounter) and media time (100ns ticks)
/// 
/// Usage pattern:
/// 1. CaptureSession creates SimpleMediaClock instance
/// 2. CaptureSession passes IMediaClockController to itself for lifecycle control
/// 3. CaptureSession passes IMediaClockWriter to IAudioInputSource for time advancement
/// 4. CaptureSession passes IMediaClockReader to video frame handlers for reading time
/// 5. Audio source calls AdvanceByAudioSamples() to drive the clock
/// 6. Video handlers call GetCurrentTime() to get synchronized timestamps
/// </summary>
class SimpleMediaClock : public IMediaClock
{
public:
    SimpleMediaClock();
    ~SimpleMediaClock() override = default;

    // Delete copy and move operations
    SimpleMediaClock(const SimpleMediaClock&) = delete;
    SimpleMediaClock& operator=(const SimpleMediaClock&) = delete;
    SimpleMediaClock(SimpleMediaClock&&) = delete;
    SimpleMediaClock& operator=(SimpleMediaClock&&) = delete;

    // IMediaClockReader implementation
    LONGLONG GetCurrentTime() const override;
    LONGLONG GetStartTime() const override;
    LONGLONG GetRelativeTime(LONGLONG qpcTimestamp) const override;
    bool IsRunning() const override;
    LONGLONG GetQpcFrequency() const override;

    // IMediaClockController implementation
    void Start(LONGLONG startQpc) override;
    void Reset() override;
    void Pause() override;
    void Resume() override;

    // IMediaClockWriter implementation
    void AdvanceByAudioSamples(UINT32 numFrames, UINT32 sampleRate) override;

private:
    // Media time in 100-nanosecond units (REFERENCE_TIME)
    std::atomic<LONGLONG> m_currentTime;

    // Recording start time in QPC units
    std::atomic<LONGLONG> m_startQpc;

    // QueryPerformanceCounter frequency (ticks per second)
    LONGLONG m_qpcFrequency;

    // Clock state flags
    std::atomic<bool> m_isRunning;
    std::atomic<bool> m_isPaused;

    // Mutex for thread-safe state transitions
    mutable std::mutex m_mutex;

    // Constants
    static constexpr LONGLONG TICKS_PER_SECOND = 10000000LL; // 100ns ticks per second

    // Helper methods
    LONGLONG QpcToTicks(LONGLONG qpcDelta) const;
};
