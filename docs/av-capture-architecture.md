# Audio/Video Capture Architecture

## Overview

This document describes the architecture for screen recording with synchronized audio and video streams in CaptureTool.

## Current Implementation (As of 2025)

### Video Capture Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│           Windows Graphics Capture API                      │
│  • Captures screen frames via Direct3D11                    │
│  • Provides frames with SystemRelativeTime timestamps       │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              FrameArrivedHandler                            │
│  • Receives frames on callback thread                       │
│  • Queues frames for background processing                  │
│  • Tracks first frame time for relative timestamps          │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              Processing Thread                              │
│  • Dequeues frames from queue                               │
│  • Calls MP4SinkWriter to encode frames                     │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              MP4SinkWriter                                  │
│  • Copies D3D11 texture to CPU-accessible staging           │
│  • Creates Media Foundation sample with timestamp           │
│  • **FIXED**: Sets fixed frame duration (30 FPS)            │
│  • Writes to H.264 encoder via IMFSinkWriter               │
└─────────────────────────────────────────────────────────────┘
```

### Audio Capture Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│           WASAPI Audio Capture                              │
│  • Captures system audio in loopback mode                   │
│  • Provides audio frames with device timestamps             │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              AudioCaptureHandler                            │
│  • Dedicated capture thread at ABOVE_NORMAL priority        │
│  • Reads audio samples from WASAPI                          │
│  • Accumulates timestamps to prevent overlaps               │
│  • Supports muting (silent samples)                         │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              MP4SinkWriter                                  │
│  • Creates Media Foundation sample with timestamp           │
│  • Calculates duration from frame count                     │
│  • Writes to AAC encoder via IMFSinkWriter                  │
└─────────────────────────────────────────────────────────────┘
```

### Synchronization

Both video and audio streams use a common recording start time (QPC timestamp):

1. The first stream to start (usually video) sets `m_recordingStartQpc` on `MP4SinkWriter`
2. The second stream reads this value for synchronization
3. Each stream calculates relative timestamps from this common base

## Critical Bug Fix (2025-12-18)

### Problem

Video recordings were very short when there was no audio because:

1. Video frame duration was calculated from timestamp deltas:
   ```cpp
   LONGLONG duration = relativeTicks - m_prevVideoTimestamp;
   ```
2. The last frame got minimal/zero duration (no "next" frame to calculate from)
3. Audio extended file duration when present, masking the bug
4. Without audio, video appeared truncated

### Solution

Use **fixed frame duration** for all video frames:

```cpp
const LONGLONG TICKS_PER_SECOND = 10000000LL;
const LONGLONG frameDuration = TICKS_PER_SECOND / 30; // 30 FPS = 333,333 ticks
sample->SetSampleDuration(frameDuration);
```

This ensures:
- Every frame (including the last) has the same duration
- Video duration is correct regardless of audio presence
- Consistent playback speed

### Why This Works

Media Foundation's IMFSinkWriter handles the final video duration calculation:
- It uses the timestamp and duration of each sample
- The last frame's duration is respected when finalizing the file
- Audio streams (if present) are properly interleaved but don't affect video timing

## Future Architecture (OBS-Style Design)

For future improvements, consider implementing an OBS-style architecture:

### Proposed Components

```
┌─────────────────────────────────────────────────────────────┐
│                     RecordingClock                          │
│  • Single source of truth for recording time                │
│  • Provides GetElapsedTicks() for all streams               │
│  • Owns target framerate and calculates frame duration      │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        ▼                                       ▼
┌──────────────────────┐            ┌──────────────────────┐
│  VideoFrameScheduler │            │  AudioSampleQueue    │
│  • Rate control      │            │  • Continuous queue  │
│  • Fixed duration    │            │  • Accumulated TS    │
└──────────┬───────────┘            └──────────┬───────────┘
           │                                   │
           └───────────────┬───────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                   MediaInterleaver                          │
│  • Normalizes timestamps to start at 0                      │
│  • Orders packets by DTS for proper muxing                  │
│  • Handles video-only mode gracefully                       │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                   MP4SinkWriter                             │
│  • Receives ordered, normalized packets                     │
│  • Simple write-through to Media Foundation                 │
└─────────────────────────────────────────────────────────────┘
```

### Benefits of Future Architecture

1. **RecordingClock**: Single source of truth for time
2. **VideoFrameScheduler**: Explicit rate control and frame dropping
3. **AudioSampleQueue**: Buffering and timestamp accumulation
4. **MediaInterleaver**: Centralized A/V synchronization
5. **Simplified MP4SinkWriter**: Just writes pre-timestamped packets

### Implementation Phases

1. **Phase 1**: Foundation (RecordingClock, MediaPacket)
2. **Phase 2**: Video Pipeline (VideoFrameScheduler)
3. **Phase 3**: Audio Pipeline (AudioSampleQueue)
4. **Phase 4**: Integration (MediaInterleaver, RecordingSession)
5. **Phase 5**: Polish (edge cases, statistics, optimization)

## Key Design Principles

### Fixed Frame Duration (Critical)

Every video frame must get a fixed duration based on target framerate:
- ✅ **Correct**: `sample->SetSampleDuration(m_frameDuration)`
- ❌ **Wrong**: `duration = relativeTicks - m_prevVideoTimestamp`

### Video-Only Mode Support

The system must work correctly without audio:
- Don't wait for audio data before writing video
- Use fixed video frame durations
- Properly finalize the last frame

### Timestamp Normalization

Both streams should start at timestamp 0:
- Track first video and audio timestamps
- Subtract offsets to normalize
- Maintain monotonic timestamp order

### Thread Safety

- Video: Event callback → Queue → Background thread → MP4SinkWriter
- Audio: Dedicated capture thread → MP4SinkWriter
- Synchronization: Common recording start time (QPC)

## References

- **Media Foundation**: Microsoft's multimedia framework
- **WASAPI**: Windows Audio Session API
- **Graphics Capture API**: Windows.Graphics.Capture
- **OBS Studio**: Reference implementation for A/V capture architecture
