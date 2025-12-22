#include "pch.h"
#include "SimpleMediaClock.h"
#include "IMediaClockAdvancer.h"

SimpleMediaClock::SimpleMediaClock()
    : m_currentTime(0)
    , m_startQpc(0)
    , m_qpcFrequency(0)
    , m_isRunning(false)
    , m_isPaused(false)
{
    LARGE_INTEGER freq;
    QueryPerformanceFrequency(&freq);
    m_qpcFrequency = freq.QuadPart;
}

// ============================================================================
// IMediaClockReader implementation
// ============================================================================

LONGLONG SimpleMediaClock::GetCurrentTime() const
{
    return m_currentTime.load();
}

LONGLONG SimpleMediaClock::GetStartTime() const
{
    return m_startQpc.load();
}

LONGLONG SimpleMediaClock::GetRelativeTime(LONGLONG qpcTimestamp) const
{
    LONGLONG startQpc = m_startQpc.load();
    if (startQpc == 0)
    {
        return 0;
    }

    LONGLONG qpcDelta = qpcTimestamp - startQpc;
    return QpcToTicks(qpcDelta);
}

bool SimpleMediaClock::IsRunning() const
{
    return m_isRunning.load();
}

LONGLONG SimpleMediaClock::GetQpcFrequency() const
{
    return m_qpcFrequency;
}

// ============================================================================
// IMediaClockController implementation
// ============================================================================

void SimpleMediaClock::Start(LONGLONG startQpc)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    m_startQpc.store(startQpc);
    m_currentTime.store(0);
    m_isRunning.store(true);
    m_isPaused.store(false);
}

void SimpleMediaClock::Reset()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    m_currentTime.store(0);
    m_startQpc.store(0);
    m_isRunning.store(false);
    m_isPaused.store(false);
}

void SimpleMediaClock::Pause()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isRunning.load())
    {
        m_isPaused.store(true);
    }
}

void SimpleMediaClock::Resume()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isRunning.load() && m_isPaused.load())
    {
        m_isPaused.store(false);
    }
}

void SimpleMediaClock::SetClockAdvancer(IMediaClockAdvancer* advancer)
{
    if (advancer)
    {
        // Provide this clock's IMediaClockWriter interface to the advancer
        // The advancer will call AdvanceByAudioSamples() to update the timeline
        advancer->SetClockWriter(this);
    }
}

// ============================================================================
// IMediaClockWriter implementation
// ============================================================================

void SimpleMediaClock::AdvanceByAudioSamples(UINT32 numFrames, UINT32 sampleRate)
{
    if (!m_isRunning.load() || m_isPaused.load())
    {
        return;
    }

    // Calculate duration of audio samples in 100ns ticks
    // Duration = (numFrames * TICKS_PER_SECOND) / sampleRate
    LONGLONG duration = (static_cast<LONGLONG>(numFrames) * TICKS_PER_SECOND) / sampleRate;

    // Atomically advance the current time
    m_currentTime.fetch_add(duration);
}

// ============================================================================
// Helper methods
// ============================================================================

LONGLONG SimpleMediaClock::QpcToTicks(LONGLONG qpcDelta) const
{
    // Convert QPC ticks to 100ns units (REFERENCE_TIME)
    // ticks = (qpcDelta * TICKS_PER_SECOND) / qpcFrequency
    return (qpcDelta * TICKS_PER_SECOND) / m_qpcFrequency;
}
