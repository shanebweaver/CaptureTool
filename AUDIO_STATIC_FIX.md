# Audio Static Fix - Technical Documentation

## Problem Statement
The AudioCaptureManager was successfully capturing audio, but the output contained significant static, crackling, and discontinuities that made the audio quality unacceptable.

## Root Cause Analysis

### Issue 1: Polling-Based Capture (Major)
**Problem**: The original implementation used `WaitForSingleObject()` with a calculated sleep time based on the audio device period. This polling approach had several issues:
- Fixed sleep intervals didn't align with actual audio buffer availability
- Caused missed audio buffers when timing was off
- Created irregular capture timing
- Resulted in audio dropouts and glitches

**Evidence**:
```cpp
// Original problematic code
DWORD sleepTime = (DWORD)(devicePeriod / HNSEC_TO_MILLISEC / SLEEP_DIVISOR);
while (m_isCapturing) {
    DWORD result = WaitForSingleObject(m_stopEvent, sleepTime);
    // Check for audio...
}
```

### Issue 2: Silent Buffer Handling (Major)
**Problem**: Silent buffers (marked with `AUDCLNT_BUFFERFLAGS_SILENT`) were being completely skipped instead of being written with zeroed data. This created gaps in the audio stream which manifested as:
- Clicks and pops at the boundaries
- Discontinuities in the timeline
- Perceived as "static" by users

**Evidence**:
```cpp
// Original problematic code
if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0) {
    m_captureClient->ReleaseBuffer(numFramesAvailable);
    continue; // SKIP - creates gap!
}
```

### Issue 3: Performance Inefficiency (Minor)
**Problem**: `QueryPerformanceFrequency()` was being called inside the capture loop on every iteration, which is unnecessary since the frequency doesn't change.

## Solution Implementation

### Fix 1: Event-Driven WASAPI Capture
Switched from polling to event-driven capture using WASAPI's built-in notification system:

1. **Added `AUDCLNT_STREAMFLAGS_EVENTCALLBACK` flag** during audio client initialization
2. **Created event handle** (`m_audioReadyEvent`) for buffer notifications
3. **Used `WaitForMultipleObjects()`** to wait on both stop event and audio ready event
4. **Process audio immediately** when notified by the system

**Benefits**:
- Precise timing - audio is processed exactly when available
- No missed buffers - system notifies us
- Lower CPU usage - no polling overhead
- Better responsiveness

**Implementation**:
```cpp
// Initialize with event callback
hr = m_audioClient->Initialize(
    AUDCLNT_SHAREMODE_SHARED,
    AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
    requestedDuration,
    0,
    mixFormat,
    nullptr
);

// Set event handle
m_audioReadyEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
m_audioClient->SetEventHandle(m_audioReadyEvent);

// Wait for events
HANDLE waitHandles[2] = { m_stopEvent, m_audioReadyEvent };
WaitForMultipleObjects(2, waitHandles, FALSE, INFINITE);
```

### Fix 2: Proper Silent Buffer Handling
Changed to write silent buffers (zeroed data) instead of skipping them:

1. **Created pre-allocated silent buffer** (std::vector<BYTE>)
2. **Resize as needed** to match audio data size
3. **Write silent samples** to maintain stream continuity

**Benefits**:
- No gaps in audio stream
- No clicks or pops
- Maintains timeline continuity
- Smooth audio output

**Implementation**:
```cpp
std::vector<BYTE> silentBuffer;
BYTE* sampleData = data;

if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0) {
    // Write silence instead of skipping
    if (silentBuffer.size() < dataSize) {
        silentBuffer.resize(dataSize, 0);
    }
    sampleData = silentBuffer.data();
}

// Always write the sample (actual or silent)
m_onAudioSample(sampleData, dataSize, relativeTimestamp);
```

### Fix 3: Performance Optimization
Moved `QueryPerformanceFrequency()` outside the loop:

```cpp
// Get QPC frequency once at start
LARGE_INTEGER qpcFreq;
QueryPerformanceFrequency(&qpcFreq);

while (m_isCapturing) {
    // Use qpcFreq without re-querying
    relativeTimestamp = (qpcDelta * 10000000LL) / qpcFreq.QuadPart;
}
```

## Testing Checklist

To verify the fix works correctly:

- [ ] Record audio while playing music/video
- [ ] Listen for static, clicks, or pops (should be absent)
- [ ] Verify audio is continuous and smooth
- [ ] Check audio/video synchronization
- [ ] Test with periods of silence (no system audio)
- [ ] Verify no audio dropouts during recording
- [ ] Monitor CPU usage (should be lower)

## Technical References

### WASAPI Event-Driven Capture
Microsoft documentation: https://docs.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream

Key points:
- `AUDCLNT_STREAMFLAGS_EVENTCALLBACK` enables event-driven mode
- Event is signaled when buffer is ready
- More efficient than polling
- Recommended approach for low-latency audio

### Buffer Flags
- `AUDCLNT_BUFFERFLAGS_SILENT`: Data pointer may be null, write silence
- `AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY`: Timeline discontinuity (informational)
- Both should be handled by maintaining stream continuity

## Commit Details

**Commit**: 1e10126
**Files Changed**: 
- AudioCaptureManager.h (added m_audioReadyEvent)
- AudioCaptureManager.cpp (rewrote Initialize, Stop, and CaptureLoop methods)

## Expected Outcome

After this fix:
1. ✅ **No static or crackling** - Event-driven capture eliminates timing issues
2. ✅ **Smooth audio** - Silent buffers maintain stream continuity
3. ✅ **Better synchronization** - Precise timing with event notifications
4. ✅ **Lower CPU usage** - No polling overhead
5. ✅ **Professional quality** - Audio output matches system playback quality
