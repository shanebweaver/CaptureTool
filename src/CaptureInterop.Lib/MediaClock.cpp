#include "pch.h"
#include "MediaClock.h"

MediaClock::MediaClock()
    : m_startQpc(0)
    , m_isStarted(false)
{
    // Initialize QPC frequency (constant for system lifetime)
    QueryPerformanceFrequency(&m_qpcFrequency);
}

MediaClock::~MediaClock()
{
    // No cleanup needed
}

bool MediaClock::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    // Check if already started (using acquire ordering to synchronize with other threads)
    if (m_isStarted.load(std::memory_order_acquire))
    {
        return false;  // Already started
    }
    
    // Capture the starting QPC timestamp
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_startQpc = qpc.QuadPart;
    
    // Mark as started (using release ordering to ensure m_startQpc is visible to other threads)
    m_isStarted.store(true, std::memory_order_release);
    
    return true;
}

void MediaClock::Reset()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    // Reset to initial state
    m_startQpc = 0;
    m_isStarted.store(false, std::memory_order_release);
}

bool MediaClock::IsStarted() const
{
    return m_isStarted.load(std::memory_order_acquire);
}

LONGLONG MediaClock::GetElapsedTime() const
{
    // Fast path: check if started without taking lock
    if (!m_isStarted.load(std::memory_order_acquire))
    {
        return 0;
    }
    
    // Get current QPC value
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    
    // Calculate elapsed QPC ticks
    LONGLONG elapsed = now.QuadPart - m_startQpc;
    
    // Convert to Media Foundation time format (100-nanosecond units)
    // MF time = (QPC ticks * 10,000,000) / QPC frequency
    const LONGLONG TICKS_PER_SECOND = 10000000LL;
    return (elapsed * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
}

LONGLONG MediaClock::GetStartQpc() const
{
    if (!m_isStarted.load(std::memory_order_acquire))
    {
        return 0;
    }
    
    return m_startQpc;
}

void MediaClock::Pause()
{
    // Reserved for future implementation
    // Not implemented in Phase 1
}

void MediaClock::Resume()
{
    // Reserved for future implementation
    // Not implemented in Phase 1
}
