# OBS-Style Capture: Quick Reference Guide

**Main Document:** [OBS-Style-Capture-Architecture-Plan.md](./OBS-Style-Capture-Architecture-Plan.md)

## Overview

This is a multi-phase project (~6 months) to add OBS-style capture capabilities to CaptureTool, enabling:
- Multiple video/audio sources
- Multi-track audio recording (up to 6 tracks like OBS)
- Per-source control (volume, mute, routing)
- Flexible audio routing and mixing
- Advanced encoder/muxer configuration

## Phase Summary

### Phase 1: Source Abstraction Layer (2-3 weeks)
**Goal:** Decouple sources from muxer via interfaces  
**Key Deliverables:**
- `IMediaSource`, `IVideoSource`, `IAudioSource` interfaces (C++ and C#)
- Refactor existing screen capture and desktop audio as sources
- Update `MP4SinkWriter` for callback-based design

### Phase 2: Multiple Source Support (3-4 weeks)
**Goal:** Add mic and per-app audio capture  
**Key Deliverables:**
- `MicrophoneAudioSource` implementation
- `ApplicationAudioSource` implementation (Windows 11+)
- `SourceManager` for registration and lifecycle
- Audio device enumeration

### Phase 3: Audio Mixer System (4-5 weeks)
**Goal:** Mix multiple audio sources with independent control  
**Key Deliverables:**
- `AudioMixer` class with volume control, muting
- Multi-track `MP4SinkWriter` (up to 6 tracks)
- Audio routing configuration
- Sample rate conversion and format normalization

### Phase 4: Advanced Muxing/Interleaving (5-6 weeks)
**Goal:** Separate encoding pipeline with better control  
**Key Deliverables:**
- `IVideoEncoder` and `IAudioEncoder` interfaces
- Support for multiple codecs (H.264, H.265)
- Improved interleaving algorithm
- Configurable encoder presets

### Phase 5: UI Enhancements (4-5 weeks, parallel with 3-4)
**Goal:** Intuitive UI for source management and routing  
**Key Deliverables:**
- Source list view with add/remove
- Audio routing matrix UI
- Per-source controls (volume sliders, meters)
- Recording configuration UI
- Live monitoring (audio levels, status)

## Key Technical Concepts

### Source Abstraction
```
Everything is a source → Video + Audio separated → Independent control
```

### Audio Routing
```
Sources (Desktop, Mic, App) → Mixer → Tracks (1-6) → MP4/MKV
```

### Synchronization
```
All sources share QPC time base → Synchronized timestamps → Perfect A/V sync
```

## Architecture Changes

### Before (Current)
```
ScreenRecorder → MP4SinkWriter (video+audio together)
     ↑                   ↑
  Video Frame      Desktop Audio Only
```

### After (Target)
```
Source Registry → Video Sources → Video Encoder ↘
                → Audio Sources → Audio Mixer → Audio Encoders → Muxer → File
                                      ↓
                                 Per-source control
                                 Multi-track routing
```

## Critical Success Factors

1. **Backward Compatibility:** Existing captures must continue working
2. **Performance:** No regression, <5% overhead per additional source
3. **Synchronization:** Perfect A/V sync across all sources
4. **User Experience:** Intuitive UI, <5 clicks for common scenarios
5. **Stability:** Graceful handling of source disconnection/errors

## Testing Priorities

1. Long-duration stability (2+ hour recordings)
2. Multiple sources simultaneously (4+ audio sources)
3. Source hot-swap during recording
4. Different Windows versions (10, 11)
5. Hardware vs. software encoding
6. Multi-track playback in popular editors

## Common Use Cases

### Use Case 1: Game Recording with Commentary
- **Video:** Game window capture
- **Audio Track 1:** Game audio (desktop)
- **Audio Track 2:** Microphone (commentary)
- **Benefit:** Edit game and voice separately in post

### Use Case 2: Tutorial with Background Music
- **Video:** Screen capture
- **Audio Track 1:** Desktop audio (system sounds)
- **Audio Track 2:** Microphone (narration)
- **Audio Track 3:** Music player app
- **Benefit:** Adjust music/voice levels independently

### Use Case 3: Live Stream Recording
- **Video:** Window capture
- **Audio Track 1:** Desktop audio
- **Audio Track 2:** Microphone
- **Audio Track 3:** Discord/Skype
- **Benefit:** Remove or enhance specific audio in editing

## Implementation Notes

### Memory Management
- Video frames: ~8MB each (1920x1080 RGBA)
- Use buffer pools and immediate encoding
- Monitor memory usage, implement backpressure

### Threading
- Each audio source: Dedicated ABOVE_NORMAL thread
- Video capture: Graphics.Capture callback thread
- Encoding: Media Foundation background threads
- UI: Standard priority, throttled updates

### Error Handling
- Source disconnection → Graceful fallback
- Encoding failure → User notification, recording continues
- Disk full → Stop recording, save what we have
- Timestamp drift → Periodic resync

## Dependencies

- **OS:** Windows 10 20H1+ (Windows 11 22H2+ for app audio)
- **APIs:** Media Foundation, WASAPI, Windows.Graphics.Capture
- **SDK:** Windows SDK 10.0.22621.0+
- **Runtime:** .NET 10, C++20

## Timeline

| Milestone | Week | Cumulative |
|-----------|------|------------|
| Phase 1 Complete | 3 | 3 weeks |
| Phase 2 Complete | 7 | 7 weeks |
| Phase 3 Complete | 12 | 12 weeks |
| Phase 4 Complete | 18 | 18 weeks |
| Phase 5 Complete | 23 | 23 weeks |
| Testing & Polish | 26 | 26 weeks (~6 months) |

## Open Questions for Implementation

1. **Codec Priority:** H.264 first, or H.265 from start?
2. **Container:** MP4 (compatibility) or MKV (better multi-track)?
3. **Audio Effects:** Add noise suppression, EQ, compression?
4. **Streaming:** Should architecture support RTMP/SRT output?
5. **GPU Encoding:** Which APIs to prioritize (NVENC, QuickSync, AMD)?
6. **Plugins:** Support third-party sources like OBS?

## Resources

- **Main Plan:** [OBS-Style-Capture-Architecture-Plan.md](./OBS-Style-Capture-Architecture-Plan.md)
- **OBS Docs:** https://docs.obsproject.com/backend-design
- **WASAPI:** https://docs.microsoft.com/en-us/windows/win32/coreaudio/
- **Media Foundation:** https://docs.microsoft.com/en-us/windows/win32/medfound/

## Next Steps

1. ✅ Review and approve plan
2. ⏳ Create Phase 1 task breakdown
3. ⏳ Set up feature branches
4. ⏳ Begin Phase 1 implementation

---

**Quick Start for Developers:**
1. Read this document
2. Review main plan for phase details
3. Understand current architecture in `/src/CaptureInterop`
4. Start with Phase 1: Source abstraction
