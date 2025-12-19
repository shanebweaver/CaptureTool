# MediaClock Architecture Design Document

## Executive Summary

This document outlines the design for a shared `MediaClock` class that will provide synchronized timing for audio and video capture in the CaptureInterop project. The MediaClock will serve as a single source of truth for elapsed recording time, eliminating timing drift between pipelines and establishing a foundation for future architectural improvements.

## Table of Contents

1. [Background and Motivation](#background-and-motivation)
2. [Current Implementation Analysis](#current-implementation-analysis)
3. [MediaClock Class Design](#mediaclock-class-design)
4. [Architecture Diagram](#architecture-diagram)
5. [Integration Plan](#integration-plan)
6. [Migration Strategy](#migration-strategy)
7. [Testing Strategy](#testing-strategy)
8. [Benefits and Impact](#benefits-and-impact)
9. [Performance Considerations](#performance-considerations)
10. [Future Enhancements](#future-enhancements)

---

## Background and Motivation

### Current State

The CaptureInterop project currently has stabilized audio and video capture pipelines, but timing is managed independently:

- **Audio Pipeline** (`AudioCaptureHandler`): Uses QueryPerformanceCounter (QPC) with dedicated timing state
- **Video Pipeline** (`FrameArrivedHandler`): Uses relative timestamps based on system time
- **Synchronization Point**: `MP4SinkWriter` stores `m_recordingStartQpc` that both pipelines access

### Problems with Current Approach

1. **Timing Drift**: Independent timing sources can drift over time, causing A/V sync issues
2. **Multiple Initialization Points**: Both pipelines independently set/check the recording start time
3. **Unclear Ownership**: `MP4SinkWriter` owns synchronization state, but it's primarily a sink, not a timing coordinator
4. **Race Conditions**: First-to-start wins race to set `m_recordingStartQpc`, which is fragile
5. **Limited Extensibility**: Difficult to add features like pause/resume or additional streams

### Goals

Create a `MediaClock` class that:

- Provides a single, authoritative source of elapsed recording time
- Uses proven QPC-based timing (from current audio pipeline)
- Supports thread-safe queries from multiple consumers
- Lays groundwork for future architecture (independent sources + muxer)
- Simplifies debugging and maintenance

---

## Current Implementation Analysis

### Audio Pipeline Timing

**File**: `AudioCaptureHandler.cpp`

```cpp
// Member variables
LONGLONG m_startQpc = 0;                    // QPC at recording start
LARGE_INTEGER m_qpcFrequency{};             // QPC frequency for conversions
LONGLONG m_nextAudioTimestamp = 0;          // Accumulated timestamp

// Initialization (lines 49-61)
if (m_sinkWriter) {
    LONGLONG sinkStartTime = m_sinkWriter->GetRecordingStartTime();
    if (sinkStartTime != 0) {
        m_startQpc = sinkStartTime;  // Video started first
    } else {
        QueryPerformanceCounter(&qpc);
        m_startQpc = qpc.QuadPart;
        m_sinkWriter->SetRecordingStartTime(m_startQpc);  // Audio first
    }
}

// Timestamp calculation (lines 144-150)
QueryPerformanceCounter(&qpc);
LONGLONG currentQpc = qpc.QuadPart;
LONGLONG elapsedQpc = currentQpc - m_startQpc;
m_nextAudioTimestamp = (elapsedQpc * TICKS_PER_SECOND) / m_qpcFrequency.QuadPart;
```

**Key Observations**:
- QPC provides high-precision timing (proven reliable)
- Converts QPC ticks to Media Foundation format (100ns ticks)
- Handles synchronization via `MP4SinkWriter`
- Maintains accumulated timestamp to prevent overlaps

### Video Pipeline Timing

**File**: `FrameArrivedHandler.cpp`

```cpp
// Member variables
std::atomic<LONGLONG> m_firstFrameSystemTime{0};

// First frame initialization (lines 136-153)
LONGLONG firstFrameTime = m_firstFrameSystemTime.load();
if (firstFrameTime == 0) {
    LONGLONG expected = 0;
    if (m_firstFrameSystemTime.compare_exchange_strong(expected, timestamp.Duration)) {
        LARGE_INTEGER qpc;
        QueryPerformanceCounter(&qpc);
        m_sinkWriter->SetRecordingStartTime(qpc.QuadPart);  // Set QPC start time
        firstFrameTime = timestamp.Duration;
    }
}

// Relative timestamp calculation (line 157)
LONGLONG relativeTimestamp = timestamp.Duration - firstFrameTime;
```

**Key Observations**:
- Uses `SystemRelativeTime` from Windows.Graphics.Capture
- Converts to relative timestamp by subtracting first frame time
- Also sets QPC start time on `MP4SinkWriter` (coordination mechanism)
- Atomic operations ensure thread-safe first-frame handling

### Synchronization Point

**File**: `MP4SinkWriter.h` and `MP4SinkWriter.cpp`

```cpp
// Member variable
LONGLONG m_recordingStartQpc = 0;  // Common start time for A/V sync

// Accessors
void SetRecordingStartTime(LONGLONG qpcStart);
LONGLONG GetRecordingStartTime() const { return m_recordingStartQpc; }
```

**Key Observations**:
- Simple storage mechanism, no timing logic
- Both pipelines can set/get this value
- First-to-start wins (whichever calls `SetRecordingStartTime` first)
- No protection against multiple sets or race conditions

### Component Initialization

**File**: `ScreenRecorderImpl.cpp`

```cpp
// StartRecording flow (lines 67-103)
1. Initialize m_sinkWriter (video configuration)
2. Initialize m_audioHandler (audio device)
3. Initialize audio stream on sink writer
4. Start audio capture (may set recording start time)
5. Register frame handler (video may set recording start time)
6. Start capture session
```

**Key Observations**:
- Audio and video initialization are interleaved
- No explicit clock initialization step
- Timing state emerges from first-to-start behavior
- No centralized lifecycle management for timing

---

## MediaClock Class Design

### Class Declaration

```cpp
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
```

### Key Design Decisions

#### 1. QPC-Based Implementation

**Rationale**: The current audio pipeline uses QPC and has proven stable. QPC provides:
- High precision (typically 100ns or better)
- Monotonic guarantees (never goes backward)
- System-wide consistency
- Low overhead

**Alternative Considered**: Using `std::chrono::steady_clock`
- **Pros**: C++ standard, portable
- **Cons**: May use QPC internally anyway on Windows; extra abstraction layer
- **Decision**: Stick with QPC for direct control and consistency with existing code

#### 2. Thread Safety Strategy

**Lock-Free Reads**: Use `std::atomic<bool>` for the started state check
- Most common operation is `GetElapsedTime()` by multiple threads
- Atomic check avoids mutex contention for hot path
- If started, QPC values are read-only (safe to access)

**Mutex-Protected Writes**: Use `std::mutex` for `Start()` and `Reset()`
- Rare operations (once per recording session)
- Need to prevent race conditions during initialization
- Acceptable to block briefly during these operations

**Implementation Pattern**:
```cpp
LONGLONG MediaClock::GetElapsedTime() const
{
    if (!m_isStarted.load(std::memory_order_acquire))
        return 0;
    
    // No lock needed - m_startQpc and m_qpcFrequency are read-only after Start()
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    LONGLONG elapsed = now.QuadPart - m_startQpc;
    return (elapsed * 10000000LL) / m_qpcFrequency.QuadPart;
}

bool MediaClock::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    if (m_isStarted.load(std::memory_order_acquire))
        return false;  // Already started
    
    LARGE_INTEGER qpc;
    QueryPerformanceCounter(&qpc);
    m_startQpc = qpc.QuadPart;
    
    m_isStarted.store(true, std::memory_order_release);
    return true;
}
```

#### 3. Ownership Model

**Stack-Allocated Member**: `MediaClock` will be a member of `MP4SinkWriter`
- **Pros**: Clear ownership, automatic lifetime management
- **Cons**: `MP4SinkWriter` becomes coordinator, not just a sink

**Alternative Considered**: `MediaClock` as member of `ScreenRecorderImpl`
- **Pros**: More logical location for coordinator role
- **Cons**: Need to pass references to both audio and video pipelines
- **Decision**: Initially make it a member of `MP4SinkWriter` to minimize changes, but design API to support later refactoring

**Access Pattern**:
```cpp
// MP4SinkWriter.h
class MP4SinkWriter {
    MediaClock m_clock;
public:
    MediaClock* GetClock() { return &m_clock; }
};

// Audio/Video handlers access via pointer
MediaClock* clock = m_sinkWriter->GetClock();
LONGLONG timestamp = clock->GetElapsedTime();
```

#### 4. API Surface

**Minimal for Phase 1**: Only essential operations
- `Start()`: Initialize clock
- `GetElapsedTime()`: Primary query method
- `GetStartQpc()`: For backward compatibility/debugging
- `IsStarted()`: State check
- `Reset()`: Cleanup for reuse

**Future Extensions**: Marked but not implemented
- `Pause()` / `Resume()`: For pause/resume feature
- `GetElapsedTimeAt(LONGLONG qpc)`: Convert arbitrary QPC to elapsed time
- `AdjustOffset(LONGLONG delta)`: Manual synchronization adjustment

#### 5. Error Handling

**Simple and Robust**: Fail-safe behavior
- `GetElapsedTime()` returns 0 if not started (safe sentinel value)
- `Start()` is idempotent (returns false if already started)
- No exceptions thrown (C++ with WinRT/COM style)
- All state transitions are atomic and well-defined

---

## Architecture Diagram

### Current Architecture (Before MediaClock)

```
┌─────────────────────────┐
│  ScreenRecorderImpl     │
│                         │
│  Initializes & manages: │
└─────────────────────────┘
         │
         ├─────────────────────┐
         │                     │
         ▼                     ▼
┌──────────────────┐  ┌──────────────────┐
│ AudioCapture     │  │ FrameArrived     │
│ Handler          │  │ Handler          │
│                  │  │                  │
│ m_startQpc       │  │ m_firstFrame     │
│ m_qpcFrequency   │  │ SystemTime       │
│ m_nextAudio      │  │                  │
│ Timestamp        │  │                  │
└──────────────────┘  └──────────────────┘
         │                     │
         │   ┌─────────────────┘
         │   │
         ▼   ▼
    ┌──────────────────────┐
    │   MP4SinkWriter      │
    │                      │
    │ m_recordingStartQpc  │◄─── Coordination Point
    │   (shared state)     │     (First-to-start wins)
    │                      │
    │ WriteAudioSample()   │
    │ WriteFrame()         │
    └──────────────────────┘
```

**Issues**:
- Timing state scattered across multiple components
- Race condition: whichever starts first sets `m_recordingStartQpc`
- `MP4SinkWriter` has dual role (sink + coordinator)
- No centralized lifecycle management

### Proposed Architecture (With MediaClock)

```
┌─────────────────────────┐
│  ScreenRecorderImpl     │
│                         │
│  Initializes & manages: │
└─────────────────────────┘
         │
         │  1. Initialize MediaClock
         │     (via MP4SinkWriter)
         │
         ├─────────────────────┐
         │                     │
         ▼                     ▼
┌──────────────────┐  ┌──────────────────┐
│ AudioCapture     │  │ FrameArrived     │
│ Handler          │  │ Handler          │
│                  │  │                  │
│ Gets clock ptr ──┼──┼──►┌──────────────┐
│                  │  │   │ MediaClock   │
│ Queries time ────┼──┼──►│              │
│                  │  │   │ m_startQpc   │
│                  │  │   │ m_qpcFreq    │
└──────────────────┘  │   │ m_isStarted  │
         │            │   │              │
         │            └──►│ Start()      │
         │                │ GetElapsed() │
         ▼                └──────────────┘
    ┌──────────────────────┐      ▲
    │   MP4SinkWriter      │      │
    │                      │      │
    │ m_clock ─────────────┘──────┘
    │   (owns MediaClock)  │
    │                      │
    │ WriteAudioSample()   │
    │ WriteFrame()         │
    └──────────────────────┘
```

**Improvements**:
- Single source of truth for timing
- Clear ownership: `MP4SinkWriter` owns clock
- No race conditions: clock started explicitly
- Consumers only query, don't modify timing state
- Foundation for future refactoring (clock could move to `ScreenRecorderImpl`)

### Timing Flow Diagram

```
Timeline:  0ms          50ms         100ms        150ms
           │            │            │            │
           ▼            ▼            ▼            ▼

Clock:     Start()      GetElapsed() GetElapsed() GetElapsed()
           │            │ = 50ms     │ = 100ms    │ = 150ms
           │            │            │            │
           ├────────────┼────────────┼────────────┤
           │            │            │            │
Audio:     Start────────►Sample──────►Sample──────►Sample
           Get Clock    ts=50ms      ts=100ms     ts=150ms
           │
Video:     ─────────────►Frame───────►Frame───────►Frame
                        ts=50ms      ts=100ms     ts=150ms

Both pipelines query same clock → guaranteed synchronization
```

---

## Integration Plan

### Phase 1: Add MediaClock Class (Foundation)

**Files to Create**:
- `src/CaptureInterop.Lib/MediaClock.h`
- `src/CaptureInterop.Lib/MediaClock.cpp`

**Files to Modify**:
- `src/CaptureInterop.Lib/CaptureInterop.Lib.vcxproj` (add new files to build)

**Implementation Steps**:

1. Create `MediaClock.h` with full class declaration
2. Implement `MediaClock.cpp`:
   - Constructor: Initialize QPC frequency
   - `Start()`: Atomically start clock
   - `GetElapsedTime()`: Lock-free time query
   - `GetStartQpc()`: Return start QPC value
   - `IsStarted()`: Check started state
   - `Reset()`: Reset to initial state
3. Add unit tests (see Testing Strategy)

**Success Criteria**: 
- MediaClock class compiles and links
- All unit tests pass
- No integration yet (standalone component)

### Phase 2: Integrate with MP4SinkWriter

**Files to Modify**:
- `src/CaptureInterop.Lib/MP4SinkWriter.h`
- `src/CaptureInterop.Lib/MP4SinkWriter.cpp`

**Changes**:

1. Add `#include "MediaClock.h"`
2. Add member variable: `MediaClock m_clock;`
3. Add accessor: `MediaClock* GetClock() { return &m_clock; }`
4. Keep `m_recordingStartQpc` temporarily (for backward compatibility)
5. In `Initialize()`: No changes (clock starts on demand)
6. In `Finalize()`: Call `m_clock.Reset()` for cleanup

**Code Example**:
```cpp
// MP4SinkWriter.h
class MP4SinkWriter {
private:
    MediaClock m_clock;
    LONGLONG m_recordingStartQpc = 0;  // Keep temporarily
    
public:
    MediaClock* GetClock() { return &m_clock; }
    
    // Keep existing methods for backward compatibility
    void SetRecordingStartTime(LONGLONG qpcStart);
    LONGLONG GetRecordingStartTime() const;
};
```

**Success Criteria**:
- Compiles without errors
- Existing tests still pass (no behavior change yet)
- Clock accessible via `GetClock()`

### Phase 3: Migrate AudioCaptureHandler

**Files to Modify**:
- `src/CaptureInterop.Lib/AudioCaptureHandler.h`
- `src/CaptureInterop.Lib/AudioCaptureHandler.cpp`

**Changes to AudioCaptureHandler.h**:
```cpp
// Remove these members:
// LONGLONG m_startQpc = 0;
// LARGE_INTEGER m_qpcFrequency{};

// Keep this (still needed for accumulated timestamp):
LONGLONG m_nextAudioTimestamp = 0;

// Add helper:
private:
    MediaClock* GetClock() const;
```

**Changes to AudioCaptureHandler.cpp**:

1. **Remove constructor code**:
   ```cpp
   // DELETE: QueryPerformanceFrequency(&m_qpcFrequency);
   ```

2. **Simplify Start() method** (lines 38-61):
   ```cpp
   // NEW VERSION:
   if (m_sinkWriter)
   {
       MediaClock* clock = m_sinkWriter->GetClock();
       if (clock)
       {
           clock->Start();  // Idempotent - safe if video already started it
       }
       
       // Keep backward compatibility: still set on sink writer
       LONGLONG startQpc = clock ? clock->GetStartQpc() : 0;
       if (startQpc != 0)
       {
           m_sinkWriter->SetRecordingStartTime(startQpc);
       }
   }
   
   m_nextAudioTimestamp = 0;  // Reset accumulator
   ```

3. **Simplify timestamp calculation** (lines 143-156):
   ```cpp
   // NEW VERSION:
   if (m_nextAudioTimestamp == 0 || m_wasDisabled)
   {
       MediaClock* clock = m_sinkWriter ? m_sinkWriter->GetClock() : nullptr;
       if (clock)
       {
           m_nextAudioTimestamp = clock->GetElapsedTime();
       }
       m_wasDisabled = false;
       m_samplesToSkip = 5;
   }
   ```

4. **Add helper method**:
   ```cpp
   MediaClock* AudioCaptureHandler::GetClock() const
   {
       return m_sinkWriter ? m_sinkWriter->GetClock() : nullptr;
   }
   ```

**Benefits**:
- Eliminates duplicate QPC state
- Removes synchronization logic from audio handler
- Clock is authoritative source

**Success Criteria**:
- Audio recording still works
- Audio/video synchronization maintained
- Existing tests pass

### Phase 4: Migrate FrameArrivedHandler

**Files to Modify**:
- `src/CaptureInterop.Lib/FrameArrivedHandler.h`
- `src/CaptureInterop.Lib/FrameArrivedHandler.cpp`

**Changes to FrameArrivedHandler.h**:
```cpp
// Remove:
// std::atomic<LONGLONG> m_firstFrameSystemTime{0};

// Add:
std::atomic<bool> m_clockStarted{false};  // Track if we started the clock
```

**Changes to FrameArrivedHandler.cpp**:

1. **Simplify Invoke() method** (lines 135-157):
   ```cpp
   // NEW VERSION:
   MediaClock* clock = m_sinkWriter ? m_sinkWriter->GetClock() : nullptr;
   if (!clock)
   {
       return E_POINTER;
   }
   
   // Start clock on first frame (idempotent)
   if (clock->Start())
   {
       // We started the clock
       m_clockStarted.store(true);
   }
   
   // Get current elapsed time from clock (replaces relative timestamp calculation)
   LONGLONG timestamp = clock->GetElapsedTime();
   
   // Queue frame with clock-based timestamp
   QueuedFrame queuedFrame;
   queuedFrame.texture = texture;
   queuedFrame.relativeTimestamp = timestamp;
   m_frameQueue.push(std::move(queuedFrame));
   ```

**Alternative Approach** (if we want to keep system time correlation):
```cpp
// First frame - establish time correlation
if (!m_clockStarted.load())
{
    if (clock->Start())
    {
        m_clockStarted.store(true);
        // Optional: Log correlation between system time and clock start
        // TimeSpan systemTime = timestamp.Duration;
        // LONGLONG clockTime = clock->GetElapsedTime();
    }
}

// Always use clock time
LONGLONG timestamp = clock->GetElapsedTime();
```

**Benefits**:
- Eliminates independent timing calculation
- Removes race condition with audio pipeline
- Clock explicitly started (not implicit via SetRecordingStartTime)

**Success Criteria**:
- Video recording still works
- Audio/video synchronization maintained
- No timing drift over long recordings

### Phase 5: Update ScreenRecorderImpl (Orchestration)

**Files to Modify**:
- `src/CaptureInterop.Lib/ScreenRecorderImpl.cpp`

**Changes**:

1. **Add clock start to StartRecording()** (after line 70):
   ```cpp
   // Initialize video sink writer
   if (!m_sinkWriter.Initialize(outputPath, device.get(), size.Width, size.Height, &hr))
   {
       return false;
   }
   
   // NEW: Explicitly start the media clock
   MediaClock* clock = m_sinkWriter.GetClock();
   if (clock)
   {
       clock->Start();  // Start clock before any capture begins
   }
   
   // Continue with audio initialization...
   ```

2. **Add clock reset to StopRecording()** (after line 141):
   ```cpp
   // Finalize MP4 file after both streams have stopped
   m_sinkWriter.Finalize();  // This will call clock.Reset() internally
   ```

**Benefits**:
- Explicit, predictable clock lifecycle
- Clock started before any capture (eliminates race)
- Clear ownership of timing initialization

**Success Criteria**:
- Recording lifecycle unchanged
- Clock state properly managed across start/stop cycles

### Phase 6: Deprecate Old Synchronization Code

**Files to Modify**:
- `src/CaptureInterop.Lib/MP4SinkWriter.h`
- `src/CaptureInterop.Lib/MP4SinkWriter.cpp`

**Changes**:

1. **Remove backward compatibility** (if all components migrated):
   ```cpp
   // DELETE from MP4SinkWriter:
   // LONGLONG m_recordingStartQpc = 0;
   // void SetRecordingStartTime(LONGLONG qpcStart);
   // LONGLONG GetRecordingStartTime() const;
   ```

2. **Update documentation**:
   - Mark old methods as deprecated
   - Add migration guide comments

**Success Criteria**:
- All code uses MediaClock
- No references to `SetRecordingStartTime()` / `GetRecordingStartTime()`
- All tests pass

---

## Migration Strategy

### Order of Implementation

1. **MediaClock Class** (standalone) - Low risk
2. **MP4SinkWriter Integration** (backward compatible) - Low risk
3. **AudioCaptureHandler Migration** (changes behavior) - Medium risk
4. **FrameArrivedHandler Migration** (changes behavior) - Medium risk
5. **ScreenRecorderImpl Updates** (orchestration) - Low risk
6. **Cleanup Old Code** (remove compatibility) - Low risk

### Rollback Strategy

Each phase maintains backward compatibility:

- **Phase 1-2**: No behavior change (can roll back trivially)
- **Phase 3**: If audio issues → revert AudioCaptureHandler, keep clock
- **Phase 4**: If video issues → revert FrameArrivedHandler, keep clock
- **Phase 5-6**: Minor changes, easy to revert

### Testing at Each Phase

- **After Phase 1**: Unit tests only
- **After Phase 2**: Existing integration tests (no behavior change)
- **After Phase 3**: Record audio-only and audio+video, verify playback
- **After Phase 4**: Record video-only and audio+video, verify A/V sync
- **After Phase 5**: Full end-to-end recording tests
- **After Phase 6**: Full regression test suite

### Compatibility Considerations

**Backward Compatibility**:
- Keep `SetRecordingStartTime()` / `GetRecordingStartTime()` during migration
- Both old and new code paths work simultaneously
- Can migrate one component at a time

**Forward Compatibility**:
- MediaClock API designed for future features (pause/resume)
- Clock can be moved to different owner later (e.g., ScreenRecorderImpl)
- Interface supports additional time query methods

### Risk Mitigation

1. **Timing Precision**: Use same QPC approach as current audio (proven)
2. **Thread Safety**: Lock-free reads, minimal contention
3. **Race Conditions**: Idempotent `Start()` eliminates races
4. **Performance**: No extra overhead vs. current implementation
5. **Testing**: Extensive unit and integration tests at each phase

---

## Testing Strategy

### Unit Tests (MediaClock)

**File**: `src/CaptureInterop.Tests/MediaClockTests.cpp`

**Test Cases**:

1. **Construction**:
   ```cpp
   TEST_CASE("MediaClock - Constructor initializes to not started")
   {
       MediaClock clock;
       REQUIRE(!clock.IsStarted());
       REQUIRE(clock.GetElapsedTime() == 0);
       REQUIRE(clock.GetStartQpc() == 0);
   }
   ```

2. **Start**:
   ```cpp
   TEST_CASE("MediaClock - Start sets started state")
   {
       MediaClock clock;
       bool result = clock.Start();
       REQUIRE(result == true);  // First start succeeds
       REQUIRE(clock.IsStarted());
       REQUIRE(clock.GetStartQpc() != 0);
   }
   
   TEST_CASE("MediaClock - Start is idempotent")
   {
       MediaClock clock;
       REQUIRE(clock.Start() == true);
       REQUIRE(clock.Start() == false);  // Second start returns false
       REQUIRE(clock.IsStarted());  // Still started
   }
   ```

3. **Timing**:
   ```cpp
   TEST_CASE("MediaClock - GetElapsedTime returns zero before start")
   {
       MediaClock clock;
       REQUIRE(clock.GetElapsedTime() == 0);
   }
   
   TEST_CASE("MediaClock - GetElapsedTime increases over time")
   {
       MediaClock clock;
       clock.Start();
       
       LONGLONG time1 = clock.GetElapsedTime();
       Sleep(10);  // 10ms
       LONGLONG time2 = clock.GetElapsedTime();
       
       REQUIRE(time2 > time1);
       
       // 10ms = 100,000 hundred-ns ticks
       // Allow 50% variance for timing jitter
       LONGLONG elapsed = time2 - time1;
       REQUIRE(elapsed >= 50000);   // At least 5ms
       REQUIRE(elapsed <= 200000);  // At most 20ms
   }
   ```

4. **Reset**:
   ```cpp
   TEST_CASE("MediaClock - Reset returns to initial state")
   {
       MediaClock clock;
       clock.Start();
       Sleep(10);
       
       REQUIRE(clock.GetElapsedTime() > 0);
       
       clock.Reset();
       
       REQUIRE(!clock.IsStarted());
       REQUIRE(clock.GetElapsedTime() == 0);
       REQUIRE(clock.GetStartQpc() == 0);
   }
   ```

5. **Thread Safety**:
   ```cpp
   TEST_CASE("MediaClock - Concurrent Start calls are safe")
   {
       MediaClock clock;
       std::atomic<int> successCount{0};
       
       std::vector<std::thread> threads;
       for (int i = 0; i < 10; i++)
       {
           threads.emplace_back([&]() {
               if (clock.Start())
                   successCount++;
           });
       }
       
       for (auto& t : threads)
           t.join();
       
       REQUIRE(successCount == 1);  // Only one thread succeeds
       REQUIRE(clock.IsStarted());
   }
   
   TEST_CASE("MediaClock - Concurrent time queries are safe")
   {
       MediaClock clock;
       clock.Start();
       
       std::atomic<bool> failed{false};
       std::vector<std::thread> threads;
       
       for (int i = 0; i < 10; i++)
       {
           threads.emplace_back([&]() {
               for (int j = 0; j < 1000; j++)
               {
                   LONGLONG time = clock.GetElapsedTime();
                   if (time < 0)
                       failed = true;
               }
           });
       }
       
       for (auto& t : threads)
           t.join();
       
       REQUIRE(!failed);
   }
   ```

6. **Precision**:
   ```cpp
   TEST_CASE("MediaClock - Provides expected precision")
   {
       MediaClock clock;
       clock.Start();
       
       // Query time multiple times with no delay
       LONGLONG time1 = clock.GetElapsedTime();
       LONGLONG time2 = clock.GetElapsedTime();
       LONGLONG time3 = clock.GetElapsedTime();
       
       // Back-to-back queries should be very close (within 1ms = 10000 ticks)
       REQUIRE(time2 - time1 < 10000);
       REQUIRE(time3 - time2 < 10000);
   }
   ```

### Integration Tests

**File**: `src/CaptureInterop.Tests/MediaClockIntegrationTests.cpp`

**Test Cases**:

1. **MP4SinkWriter Integration**:
   ```cpp
   TEST_CASE("MediaClock - Accessible via MP4SinkWriter")
   {
       // Setup: Create D3D device (test helper)
       auto device = CreateTestD3DDevice();
       
       MP4SinkWriter writer;
       writer.Initialize(L"test.mp4", device.get(), 1920, 1080);
       
       MediaClock* clock = writer.GetClock();
       REQUIRE(clock != nullptr);
       REQUIRE(!clock->IsStarted());
   }
   ```

2. **Audio/Video Synchronization**:
   ```cpp
   TEST_CASE("MediaClock - Audio and video use same time base")
   {
       // Create mock audio and video handlers
       // Both query clock
       // Verify timestamps are consistent
       
       MediaClock clock;
       clock.Start();
       
       // Simulate audio query
       LONGLONG audioTime = clock.GetElapsedTime();
       
       // Simulate video query (shortly after)
       LONGLONG videoTime = clock.GetElapsedTime();
       
       // Should be nearly identical or video slightly ahead
       REQUIRE(videoTime >= audioTime);
       REQUIRE(videoTime - audioTime < 10000);  // Less than 1ms
   }
   ```

### Manual Testing Scenarios

1. **Short Recording (30 seconds)**:
   - Record with audio + video
   - Verify A/V sync at start, middle, end
   - Check for drift or glitches

2. **Long Recording (10+ minutes)**:
   - Record with audio + video
   - Verify no drift accumulates
   - Check CPU/memory usage (should match baseline)

3. **Toggle Audio**:
   - Start recording with audio
   - Mute audio during recording
   - Unmute audio
   - Verify synchronization maintained

4. **Audio-Only Recording**:
   - Record audio without video
   - Verify clock still works correctly

5. **Video-Only Recording**:
   - Record video without audio
   - Verify clock still works correctly

6. **Start/Stop Cycles**:
   - Start and stop recording multiple times
   - Verify clock resets properly
   - Check for resource leaks

### Performance Testing

**Metrics to Measure**:

1. **Latency**: Time to call `GetElapsedTime()`
   - **Target**: < 1 microsecond (same as QPC)
   - **Method**: Benchmark with 1M calls

2. **Contention**: Multi-threaded access
   - **Target**: No blocking on reads
   - **Method**: 10 threads querying simultaneously

3. **Accuracy**: Compare to QPC directly
   - **Target**: Identical to direct QPC conversion
   - **Method**: Verify math matches existing code

4. **Memory**: Size of MediaClock
   - **Target**: < 64 bytes
   - **Method**: `sizeof(MediaClock)`

---

## Benefits and Impact

### Benefits

1. **Eliminates Timing Drift**:
   - Single clock source prevents divergence
   - Consistent time base for all streams
   - Long recordings maintain sync

2. **Simplified Initialization**:
   - One clock start call (explicit)
   - No race conditions
   - Clear lifecycle management

3. **Better Debugging**:
   - Single point to log/inspect time
   - Easier to add diagnostics
   - Clear ownership of timing state

4. **Foundation for Future**:
   - Supports pause/resume
   - Enables independent source architecture
   - Facilitates multiple stream types

5. **Cleaner Code**:
   - Less duplicate state
   - Clear separation of concerns
   - Reduced coupling

### Impact on Existing Code

**Minimal Disruption**:
- Phased migration strategy
- Backward compatibility during transition
- No API changes to public interfaces

**Code Reduction**:
- Remove ~30 lines from AudioCaptureHandler
- Remove ~20 lines from FrameArrivedHandler
- Add ~150 lines for MediaClock (net increase ~100 lines)

**Maintenance**:
- Easier to understand timing logic
- Centralized place to fix timing bugs
- Less state to track across components

---

## Performance Considerations

### CPU Overhead

**Current Implementation**:
- Each pipeline queries QPC independently
- Audio: ~2-3 QPC calls per sample (10ms intervals)
- Video: 1 QPC call per frame (30 fps)

**MediaClock Implementation**:
- Same number of QPC calls
- Extra indirection: one pointer dereference
- Atomic load for started check

**Estimated Impact**: < 1% (negligible)

### Memory Overhead

**Current State**:
- AudioCaptureHandler: 24 bytes (3 LONGLONGs)
- FrameArrivedHandler: 8 bytes (1 atomic LONGLONG)
- MP4SinkWriter: 8 bytes (1 LONGLONG)
- **Total**: 40 bytes

**With MediaClock**:
- MediaClock: ~48 bytes (2 LONGLONGs + mutex + atomic)
- AudioCaptureHandler: 8 bytes (removed 2 LONGLONGs)
- FrameArrivedHandler: 1 byte (atomic bool)
- MP4SinkWriter: 8 bytes (backward compat, can remove later)
- **Total**: 65 bytes

**Increase**: +25 bytes per recording session (negligible)

### Thread Contention

**Lock-Free Reads**:
- `GetElapsedTime()` uses atomic check + direct read
- No mutex acquisition on hot path
- Same performance as current implementation

**Locked Writes**:
- `Start()` / `Reset()` use mutex
- Called once per session (rare)
- Acceptable to block briefly

**Conclusion**: No measurable performance impact

---

## Future Enhancements

### Pause/Resume Support

**Design**:
```cpp
class MediaClock {
    LONGLONG m_pausedQpc;              // QPC when paused
    LONGLONG m_totalPausedDuration;    // Accumulated pause time
    std::atomic<bool> m_isPaused;      // Current pause state
    
public:
    void Pause() {
        if (!m_isPaused) {
            QueryPerformanceCounter(&m_pausedQpc);
            m_isPaused = true;
        }
    }
    
    void Resume() {
        if (m_isPaused) {
            LARGE_INTEGER now;
            QueryPerformanceCounter(&now);
            m_totalPausedDuration += now.QuadPart - m_pausedQpc;
            m_isPaused = false;
        }
    }
    
    LONGLONG GetElapsedTime() const {
        if (!m_isStarted) return 0;
        
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        
        LONGLONG elapsed = now.QuadPart - m_startQpc - m_totalPausedDuration;
        
        if (m_isPaused) {
            // During pause, freeze time at pause point
            elapsed -= (now.QuadPart - m_pausedQpc);
        }
        
        return (elapsed * 10000000LL) / m_qpcFrequency.QuadPart;
    }
};
```

**Integration**:
- `ScreenRecorderImpl::PauseRecording()` → `clock->Pause()`
- `ScreenRecorderImpl::ResumeRecording()` → `clock->Resume()`
- Audio/video handlers transparently see frozen time

### Independent Source Architecture

**Vision**:
```
┌────────────────┐
│ ScreenRecorder │
│                │
│  m_clock ──────┼──────┐
└────────────────┘      │
                        │
    ┌───────────────────┼───────────────┐
    │                   │               │
    ▼                   ▼               ▼
┌─────────┐       ┌─────────┐     ┌─────────┐
│  Audio  │       │  Video  │     │  Other  │
│ Source  │       │ Source  │     │ Source  │
└─────────┘       └─────────┘     └─────────┘
    │                   │               │
    └───────────────────┼───────────────┘
                        │
                        ▼
                   ┌─────────┐
                   │  Muxer  │
                   └─────────┘
```

**Benefits**:
- Sources are independent, testable modules
- Muxer handles synchronization logic
- Easy to add new source types (microphone, screen region, etc.)
- Clock owned by top-level coordinator

### Multiple Clock Support

**Use Case**: Recording multiple monitors simultaneously

**Design**:
```cpp
class MediaClockManager {
    std::vector<std::unique_ptr<MediaClock>> m_clocks;
    
public:
    MediaClock* CreateClock();
    void SynchronizeClocks();  // Align multiple clocks
};
```

### Time Adjustment

**Use Case**: Manual synchronization correction

**API**:
```cpp
class MediaClock {
public:
    void AdjustOffset(LONGLONG deltaInTicks);
    LONGLONG GetOffset() const;
};
```

**Usage**: If A/V drift is detected, adjust clock offset to compensate

---

## Appendices

### Appendix A: QPC vs. Other Timing Methods

| Method | Precision | Monotonic | Overhead | Portability |
|--------|-----------|-----------|----------|-------------|
| QueryPerformanceCounter | ~100ns | Yes | ~50ns | Windows only |
| GetTickCount64 | 10-16ms | Yes | ~5ns | Windows only |
| timeGetTime | 1-5ms | No | ~10ns | Windows only |
| std::chrono::steady_clock | Varies | Yes | ~50ns | Cross-platform |
| std::chrono::high_resolution_clock | Varies | No | ~50ns | Cross-platform |

**Decision**: Use QPC for consistency with existing code and guaranteed precision.

### Appendix B: Media Foundation Time Format

- **Unit**: 100-nanosecond ticks
- **Range**: 0 to ~29,000 years (LONGLONG)
- **Conversion**: `ticks = seconds × 10,000,000`
- **Example**: 1 second = 10,000,000 ticks
- **Example**: 1ms = 10,000 ticks
- **Example**: 1 frame @ 30fps = 333,333 ticks

### Appendix C: Thread Safety Patterns

**Lock-Free Read Pattern**:
```cpp
// Fast path: no lock needed
if (m_isStarted.load(std::memory_order_acquire)) {
    // Read-only access to m_startQpc, m_qpcFrequency
    return ComputeTime();
}
return 0;
```

**Mutex Write Pattern**:
```cpp
// Slow path: lock needed
std::lock_guard<std::mutex> lock(m_mutex);
if (m_isStarted.load(std::memory_order_acquire))
    return false;  // Already started
    
// Modify state
m_startQpc = GetCurrentQpc();
m_isStarted.store(true, std::memory_order_release);
return true;
```

### Appendix D: References

- [QueryPerformanceCounter Documentation](https://docs.microsoft.com/en-us/windows/win32/api/profileapi/nf-profileapi-queryperformancecounter)
- [Media Foundation Time Format](https://docs.microsoft.com/en-us/windows/win32/medfound/media-foundation-time)
- [C++ Atomics and Memory Ordering](https://en.cppreference.com/w/cpp/atomic/memory_order)
- [Windows Graphics Capture API](https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture)

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-19 | AI Assistant | Initial design document |

## Reviewers

- [ ] Architecture Review
- [ ] Performance Review
- [ ] Security Review
- [ ] Code Review (after implementation)

## Next Steps

1. **Review this document** with stakeholders
2. **Refine design** based on feedback
3. **Approve for implementation**
4. **Begin Phase 1**: Implement MediaClock class
5. **Iterate through phases** with testing at each step
6. **Final review** after complete migration

---

*End of Document*
