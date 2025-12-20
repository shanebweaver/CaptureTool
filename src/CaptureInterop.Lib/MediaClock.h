#pragma once

/// <summary>
/// Thread-safe clock for synchronized media capture timing.
/// Provides a single source of truth for elapsed recording time using QPC.
/// Designed for use by multiple concurrent capture streams.
/// </summary>
class MediaClock
{
public:
    MediaClock();
    ~MediaClock();

    // Lifecycle Management
    
    /// <summary>
    /// Start the clock and establish the time base.
    /// Must be called before any timing queries.
    /// Thread-safe: Only the first call will start the clock.
    /// </summary>
    /// <returns>True if clock was started by this call, false if already started.</returns>
    bool Start();
    
    /// <summary>
    /// Reset the clock to initial state.
    /// WARNING: Should only be called when all consumers have stopped.
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Check if the clock has been started.
    /// </summary>
    /// <returns>True if Start() has been called, false otherwise.</returns>
    bool IsStarted() const;

    // Time Queries
    
    /// <summary>
    /// Get elapsed time since Start() in Media Foundation format.
    /// </summary>
    /// <returns>Elapsed time in 100-nanosecond units, or 0 if not started.</returns>
    LONGLONG GetElapsedTime() const;
    
    /// <summary>
    /// Get the QPC timestamp when the clock was started.
    /// Useful for consumers that need the raw QPC value.
    /// </summary>
    /// <returns>QPC timestamp at Start(), or 0 if not started.</returns>
    LONGLONG GetStartQpc() const;

    // Future-Proofing (Not Implemented in Phase 1)
    
    /// <summary>
    /// Pause the clock (stops time accumulation).
    /// Reserved for future implementation.
    /// </summary>
    void Pause();
    
    /// <summary>
    /// Resume the clock after pause.
    /// Reserved for future implementation.
    /// </summary>
    void Resume();

private:
    // QPC-based timing state
    LONGLONG m_startQpc;                 // QPC timestamp at Start()
    LARGE_INTEGER m_qpcFrequency;        // QPC frequency for conversions
    
    // Thread safety
    mutable std::mutex m_mutex;          // Protects state during Start/Reset
    std::atomic<bool> m_isStarted;       // Lock-free check for started state
    
    // Future: Pause/resume state
    // LONGLONG m_pausedQpc;
    // LONGLONG m_totalPausedDuration;
    // std::atomic<bool> m_isPaused;
};
