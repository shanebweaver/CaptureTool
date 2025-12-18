# OBS-Style Capture Architecture Implementation Plan

**Date:** 2025-12-18  
**Project:** CaptureTool  
**Goal:** Implement OBS-style capture with separate video and audio sources, multi-track support, and advanced interleaving/muxing

## Executive Summary

This document outlines a multi-phase plan to transform CaptureTool's current monolithic capture architecture into a modular, OBS-style system that supports:
- Multiple independent video and audio sources
- Per-source configuration and control
- Multi-track audio recording
- Flexible routing and mixing
- Advanced muxing with separate track management

## Current Architecture Analysis

### What We Have

**C++ Native Layer (CaptureInterop):**
- `ScreenRecorder.cpp/h`: Main recording coordinator
- `MP4SinkWriter`: Single unified video+audio muxer (H.264 + AAC)
- `AudioCaptureHandler`: Thread-based audio capture (WASAPI loopback only)
- `AudioCaptureDevice`: WASAPI device wrapper
- `FrameArrivedHandler`: Video frame capture via Windows.Graphics.Capture

**C# Domain Layer:**
- `IVideoCaptureHandler`: High-level capture interface
- `WindowsScreenRecorder`: P/Invoke wrapper to CaptureInterop
- Single boolean flag for desktop audio enable/disable

**Key Characteristics:**
- Tightly coupled video+audio capture
- Single audio source (desktop audio loopback)
- All-or-nothing audio recording
- Direct muxing during capture (no intermediate separation)
- Global state management in C++ static variables
- Synchronized start via shared `recordingStartQpc`

### Strengths
✅ Working synchronization between video and audio
✅ Efficient real-time H.264 encoding via Media Foundation
✅ Thread-safe audio capture with proper timestamp management
✅ Cross-language integration (C++/WinRT ↔ C#)

### Limitations
❌ No support for multiple audio sources (e.g., mic + desktop separately)
❌ Cannot record audio-only or video-only tracks
❌ No per-source volume/effects control
❌ Single audio track in output (no multi-track like OBS)
❌ Cannot toggle individual sources during recording
❌ Monolithic architecture makes extension difficult

## OBS Architecture Insights

### Key Design Principles

1. **Source Abstraction**
   - Everything is a source (video capture, audio capture, filters, scenes)
   - Sources implement capability flags (video/audio/async)
   - Hierarchical composition (scenes contain sources)

2. **Separation of Concerns**
   - Independent video and audio pipelines
   - Separate threads for graphics, audio, and encoding
   - Sources output to pipelines, not directly to files

3. **Multi-Track Support**
   - Up to 6 separate audio tracks in output files
   - Per-source track assignment via routing
   - Independent processing per track

4. **Flexible Routing**
   - Audio mixer with multiple channels
   - Per-source volume, filters, and effects
   - Scene-based composition for complex layouts

5. **Muxing Strategy**
   - Separate encoding of video and audio streams
   - Container format (MKV/MP4) combines tracks
   - Preserves track separation for post-production

## Multi-Phase Implementation Plan

---

## Phase 1: Source Abstraction Layer

**Goal:** Create a flexible source abstraction that decouples capture sources from the muxer.

### 1.1 Define Core Interfaces

**C++ Layer:**
```cpp
// IMediaSource.h - Base interface for all sources
class IMediaSource {
public:
    enum SourceType { Video, Audio };
    virtual SourceType GetSourceType() = 0;
    virtual bool Initialize() = 0;
    virtual bool Start() = 0;
    virtual void Stop() = 0;
    virtual bool IsRunning() = 0;
};

// IVideoSource.h
class IVideoSource : public IMediaSource {
public:
    virtual void SetFrameCallback(std::function<void(ID3D11Texture2D*, LONGLONG)>) = 0;
    virtual void GetResolution(UINT32& width, UINT32& height) = 0;
};

// IAudioSource.h
class IAudioSource : public IMediaSource {
public:
    virtual void SetAudioCallback(std::function<void(const BYTE*, UINT32, LONGLONG)>) = 0;
    virtual WAVEFORMATEX* GetFormat() = 0;
    virtual void SetEnabled(bool enabled) = 0;
};
```

**C# Layer:**
```csharp
// IMediaSource.cs
public interface IMediaSource
{
    string Id { get; }
    string Name { get; }
    MediaSourceType Type { get; }
    bool IsActive { get; }
    
    Task<bool> InitializeAsync();
    Task StartAsync();
    Task StopAsync();
}

// IVideoSource.cs
public interface IVideoSource : IMediaSource
{
    int Width { get; }
    int Height { get; }
}

// IAudioSource.cs
public interface IAudioSource : IMediaSource
{
    AudioSourceType SourceType { get; } // Desktop, Microphone, Application
    float Volume { get; set; }
    bool IsMuted { get; set; }
}
```

### 1.2 Refactor Existing Implementations

**Tasks:**
- Extract current screen capture into `ScreenCaptureSource : IVideoSource`
- Extract current desktop audio into `DesktopAudioSource : IAudioSource`
- Modify `MP4SinkWriter` to accept frames/samples from callbacks instead of direct coupling
- Update `ScreenRecorder` to coordinate multiple sources

**Files to Modify:**
- `CaptureInterop/ScreenRecorder.cpp/h`
- `CaptureInterop/AudioCaptureHandler.cpp/h`
- `CaptureInterop/MP4SinkWriter.cpp/h`
- New: `CaptureInterop/IMediaSource.h`
- New: `CaptureInterop/ScreenCaptureSource.cpp/h`
- New: `CaptureInterop/DesktopAudioSource.cpp/h`

### 1.3 Update API Surface

**New C++ Exports:**
```cpp
// Source management
__declspec(dllexport) void* CreateVideoSource(SourceConfig* config);
__declspec(dllexport) void* CreateAudioSource(AudioSourceConfig* config);
__declspec(dllexport) bool StartSource(void* sourceHandle);
__declspec(dllexport) void StopSource(void* sourceHandle);
__declspec(dllexport) void ReleaseSource(void* sourceHandle);

// Recording with multiple sources
__declspec(dllexport) bool StartRecordingWithSources(
    const wchar_t* outputPath,
    void** videoSources, int videoSourceCount,
    void** audioSources, int audioSourceCount
);
```

**Estimated Effort:** 2-3 weeks  
**Risk Level:** Medium (requires careful refactoring of working code)  
**Dependencies:** None

---

## Phase 2: Multiple Source Support

**Goal:** Enable multiple audio sources to be captured simultaneously.

### 2.1 Microphone Audio Source

**Implementation:**
- Create `MicrophoneAudioSource : IAudioSource`
- Support device enumeration and selection
- Independent capture thread per audio source
- Synchronized timestamps with recording start time

**Files to Create:**
- `CaptureInterop/MicrophoneAudioSource.cpp/h`
- `CaptureTool.Domains.Capture.Interfaces/IAudioDeviceEnumerator.cs`

### 2.2 Per-Application Audio Capture

**Implementation:**
- Windows 10+ Audio Session API integration
- Enumerate running applications with audio
- Create `ApplicationAudioSource : IAudioSource`
- Per-process audio isolation

**Technical Challenges:**
- Requires Windows 11 22H2+ for best support
- Fallback for older Windows versions
- Process lifecycle management

**Files to Create:**
- `CaptureInterop/ApplicationAudioSource.cpp/h`
- `CaptureInterop/AudioSessionEnumerator.cpp/h`

### 2.3 Source Registry and Management

**Implementation:**
- `SourceManager` class to register and coordinate all sources
- Source lifecycle management
- Thread-safe source addition/removal during recording
- Reference counting and cleanup

**C++ Implementation:**
```cpp
class SourceManager {
public:
    static SourceManager& Instance();
    
    SourceHandle RegisterSource(std::unique_ptr<IMediaSource> source);
    void UnregisterSource(SourceHandle handle);
    
    std::vector<IVideoSource*> GetVideoSources();
    std::vector<IAudioSource*> GetAudioSources();
    
    void StartAll();
    void StopAll();
};
```

**C# Layer:**
```csharp
public interface ISourceRegistry
{
    IReadOnlyList<IVideoSource> VideoSources { get; }
    IReadOnlyList<IAudioSource> AudioSources { get; }
    
    Task<IVideoSource> CreateScreenCaptureSourceAsync(MonitorInfo monitor);
    Task<IAudioSource> CreateDesktopAudioSourceAsync();
    Task<IAudioSource> CreateMicrophoneSourceAsync(string deviceId);
    Task<IAudioSource> CreateApplicationAudioSourceAsync(int processId);
    
    void RemoveSource(IMediaSource source);
}
```

**Estimated Effort:** 3-4 weeks  
**Risk Level:** Medium-High (complex state management, new WASAPI scenarios)  
**Dependencies:** Phase 1 complete

---

## Phase 3: Audio Mixer System

**Goal:** Implement an audio mixer that can combine multiple audio sources with independent control.

### 3.1 Audio Mixer Core

**Implementation:**
- `AudioMixer` class that combines multiple audio sources
- Per-source volume control and muting
- Sample rate conversion and format normalization
- Real-time mixing with minimal latency

**Technical Details:**
```cpp
class AudioMixer {
public:
    void AddSource(IAudioSource* source, int trackIndex);
    void RemoveSource(IAudioSource* source);
    
    void SetSourceVolume(IAudioSource* source, float volume); // 0.0 - 1.0
    void SetSourceMuted(IAudioSource* source, bool muted);
    
    // Output mixed audio to sink writer
    void SetOutputCallback(std::function<void(const BYTE*, UINT32, LONGLONG, int track)>);
};
```

**Key Features:**
- Support for different sample rates (automatic resampling)
- Different channel counts (mono/stereo conversion)
- Volume scaling without clipping
- Zero-copy optimization where possible

### 3.2 Multi-Track Recording

**Implementation:**
- Extend `MP4SinkWriter` to support multiple audio streams
- Up to 6 audio tracks (matching OBS)
- Per-track routing configuration
- Track metadata (names, language, etc.)

**MP4SinkWriter Changes:**
```cpp
bool InitializeAudioStream(
    WAVEFORMATEX* audioFormat,
    int trackIndex,
    const wchar_t* trackName,
    HRESULT* outHr = nullptr
);

HRESULT WriteAudioSample(
    const BYTE* pData,
    UINT32 numFrames,
    LONGLONG timestamp,
    int trackIndex  // NEW: specify which track
);
```

**Container Format:**
- Primary: MP4 with multiple AAC audio tracks
- Alternative: MKV for better multi-track support (future)

### 3.3 Audio Routing Configuration

**C# Layer:**
```csharp
public class AudioRoutingConfig
{
    public Dictionary<string, int> SourceToTrackMapping { get; set; }
    public Dictionary<int, string> TrackNames { get; set; }
    public Dictionary<string, float> SourceVolumes { get; set; }
}

public interface IAudioRoutingService
{
    void ConfigureRouting(AudioRoutingConfig config);
    void AssignSourceToTrack(IAudioSource source, int trackIndex);
    void SetSourceVolume(IAudioSource source, float volume);
}
```

**Estimated Effort:** 4-5 weeks  
**Risk Level:** High (complex real-time audio processing, synchronization challenges)  
**Dependencies:** Phase 2 complete

---

## Phase 4: Advanced Muxing and Interleaving

**Goal:** Improve the muxing pipeline to better handle multiple tracks and provide more control.

### 4.1 Separate Encoding Pipeline

**Current State:**
- Video: Direct to H.264 encoder in `MP4SinkWriter`
- Audio: Direct to AAC encoder in `MP4SinkWriter`

**Proposed Architecture:**
```
Sources → Encoders → Muxer → File
         ↓
    [Video Encoder]  [Audio Encoder 1]  [Audio Encoder N]
         ↓                  ↓                  ↓
              [Interleaver/Muxer]
                     ↓
                 [MP4/MKV File]
```

**Implementation:**
- Separate `IVideoEncoder` and `IAudioEncoder` interfaces
- Decouple encoding from muxing
- Support encoder presets (quality/performance tradeoffs)
- Support different codecs (H.264, H.265, AV1 future)

### 4.2 Advanced Interleaving

**Features:**
- Proper A/V interleaving for streaming efficiency
- Buffering strategy to prevent drift
- Configurable interleave duration
- B-frame support for advanced video encoding

**Technical Considerations:**
- Media Foundation API limitations
- Timestamp precision (100ns units)
- Buffer management to prevent OOM
- Seek table optimization

### 4.3 Recording Pipeline Configuration

**C# Layer:**
```csharp
public class RecordingConfig
{
    public VideoEncoderConfig Video { get; set; }
    public List<AudioTrackConfig> AudioTracks { get; set; }
    public MuxerConfig Muxer { get; set; }
}

public class VideoEncoderConfig
{
    public VideoCodec Codec { get; set; } // H264, H265, AV1
    public int Bitrate { get; set; }
    public string Preset { get; set; } // fast, medium, slow
    public bool HardwareAcceleration { get; set; }
}

public class AudioTrackConfig
{
    public List<IAudioSource> Sources { get; set; }
    public string TrackName { get; set; }
    public AudioCodec Codec { get; set; } // AAC, Opus, PCM
    public int Bitrate { get; set; }
}

public class MuxerConfig
{
    public ContainerFormat Format { get; set; } // MP4, MKV
    public TimeSpan InterleaveDuration { get; set; }
}
```

**Estimated Effort:** 5-6 weeks  
**Risk Level:** High (requires deep Media Foundation knowledge, potential codec issues)  
**Dependencies:** Phase 3 complete

---

## Phase 5: UI and User Experience

**Goal:** Provide intuitive UI for managing sources, routing, and recording.

### 5.1 Source Management UI

**Features:**
- List of available video/audio sources
- Add/remove sources
- Source preview (audio level meters, video thumbnails)
- Per-source controls (volume, mute, enable/disable)

**UI Components:**
- `SourceListView`: ListView of all sources
- `AudioSourceControl`: Volume slider, mute button, level meter
- `VideoSourceControl`: Resolution display, capture region selector

### 5.2 Audio Routing UI

**Features:**
- Visual routing matrix (sources → tracks)
- Drag-and-drop track assignment
- Track naming and configuration
- Audio mixing controls per track

**UI Mockup:**
```
┌─────────────────────────────────────────┐
│ Audio Routing                           │
├─────────────┬───────┬───────┬───────────┤
│ Source      │ Trk 1 │ Trk 2 │ Volume    │
├─────────────┼───────┼───────┼───────────┤
│ Desktop     │   ☑   │       │ ████░░    │
│ Microphone  │       │   ☑   │ ███░░░    │
│ Discord     │   ☑   │       │ ██░░░░    │
└─────────────┴───────┴───────┴───────────┘
```

### 5.3 Recording Configuration UI

**Features:**
- Encoder settings (preset, bitrate, codec)
- Output format selection (MP4, MKV)
- Track configuration
- Preset management (save/load configurations)

**Settings Pages:**
- `VideoEncoderSettingsPage`
- `AudioRoutingSettingsPage`
- `RecordingPresetsPage`

### 5.4 Live Monitoring

**Features:**
- Real-time audio level meters for each source
- Recording status indicators
- Dropped frame warnings
- Disk space monitoring

**Implementation:**
- Event-based updates from C++ layer
- Throttled UI updates (60fps max)
- Background monitoring service

**Estimated Effort:** 4-5 weeks  
**Risk Level:** Medium (mostly UI work, depends on backend stability)  
**Dependencies:** Phases 1-4 (can start after Phase 2)

---

## Implementation Considerations

### Technical Challenges

1. **Thread Synchronization**
   - Multiple audio capture threads
   - Video capture thread
   - Encoding threads
   - Muxing thread
   - Solution: Lock-free queues, atomic operations, careful timestamp management

2. **Timestamp Synchronization**
   - All sources must share common time base (QPC)
   - Handle clock drift over long recordings
   - Resynchronization after pause/resume
   - Solution: Reference clock system, periodic sync points

3. **Memory Management**
   - Video frames are large (1920x1080x4 = 8MB each)
   - Audio buffers accumulate quickly
   - Multiple sources multiply memory usage
   - Solution: Buffer pools, immediate encoding, backpressure handling

4. **Performance**
   - Multiple audio resampling operations
   - Real-time encoding must keep up with capture
   - UI must remain responsive
   - Solution: Hardware encoding, optimized audio paths, thread priorities

5. **Error Handling**
   - Source disconnection (unplugged mic, process exit)
   - Encoding failures
   - Disk full scenarios
   - Solution: Graceful degradation, source hot-swapping, user notifications

### Testing Strategy

1. **Unit Tests**
   - Source lifecycle management
   - Audio mixing algorithm
   - Timestamp calculations
   - Routing logic

2. **Integration Tests**
   - Single source recording (baseline)
   - Multi-source recording
   - Source add/remove during recording
   - Long-duration stability tests

3. **Performance Tests**
   - CPU usage with N sources
   - Memory usage over time
   - Encoding throughput
   - Latency measurements

4. **Compatibility Tests**
   - Different Windows versions (10, 11)
   - Different audio devices
   - Different screen resolutions
   - Hardware encoding vs software

### Migration Strategy

**Backward Compatibility:**
- Phase 1 must maintain existing API for current features
- Existing recordings must still work
- Settings migration for new source system

**Gradual Rollout:**
- Ship Phase 1 as internal refactor (no user-visible changes)
- Phase 2: "Beta" feature flag for multi-source support
- Phases 3-4: Opt-in advanced features
- Phase 5: Full rollout with new UI

**Feature Flags:**
```csharp
public enum FeatureFlag
{
    MultiSourceCapture,
    MultiTrackRecording,
    AdvancedAudioRouting,
    ApplicationAudioCapture
}
```

### Dependencies and Prerequisites

**Libraries:**
- Windows Media Foundation (already in use)
- WASAPI (already in use)
- Windows.Graphics.Capture (already in use)
- Media Foundation Transforms for encoding

**Windows Versions:**
- Minimum: Windows 10 20H1 (existing minimum)
- Recommended: Windows 11 22H2+ (for app audio capture)

**Development Tools:**
- Visual Studio 2022 17.8+
- C++20 support
- .NET 10 SDK
- Windows SDK 10.0.22621.0+

---

## Success Metrics

**Phase 1:**
- [ ] All existing capture scenarios still work
- [ ] Zero performance regression
- [ ] Cleaner code architecture (measured by coupling metrics)

**Phase 2:**
- [ ] Can record desktop audio + microphone simultaneously
- [ ] Can record per-application audio (Windows 11)
- [ ] <5% CPU overhead for additional audio source

**Phase 3:**
- [ ] Can assign sources to multiple tracks
- [ ] Per-source volume control works without distortion
- [ ] Multi-track MP4 playback verified in VLC, Premiere, DaVinci

**Phase 4:**
- [ ] Configurable encoder settings
- [ ] Support both MP4 and MKV containers
- [ ] <100ms additional latency for encoder pipeline

**Phase 5:**
- [ ] Intuitive source management UI
- [ ] Audio routing UI tested with non-technical users
- [ ] <5 clicks to configure common scenarios

---

## Alternative Approaches Considered

### 1. Fork OBS Studio
**Pros:** Proven architecture, extensive features  
**Cons:** Massive codebase, different tech stack, license complexity  
**Decision:** Rejected - too heavyweight for CaptureTool's focused scope

### 2. Use Third-Party Libraries
**Pros:** Faster development, maintained by others  
**Cons:** External dependencies, potential licensing issues, less control  
**Decision:** Rejected - prefer direct Windows API usage for better control

### 3. Separate Video/Audio Files
**Pros:** Simpler implementation, no muxing complexity  
**Cons:** Poor user experience, sync issues, extra post-processing needed  
**Decision:** Rejected - OBS-style muxing is core requirement

### 4. Complete Rewrite
**Pros:** Clean slate, optimal architecture from start  
**Cons:** High risk, would break existing features, long timeline  
**Decision:** Rejected - phased approach is safer and more practical

---

## Timeline Estimate

| Phase | Duration | Start After | Cumulative |
|-------|----------|-------------|------------|
| Phase 1: Source Abstraction | 2-3 weeks | Approval | 3 weeks |
| Phase 2: Multiple Sources | 3-4 weeks | Phase 1 | 7 weeks |
| Phase 3: Audio Mixer | 4-5 weeks | Phase 2 | 12 weeks |
| Phase 4: Advanced Muxing | 5-6 weeks | Phase 3 | 18 weeks |
| Phase 5: UI/UX | 4-5 weeks | Phase 2* | 23 weeks |
| Testing & Polish | 2-3 weeks | All phases | 26 weeks |

**Total: ~6 months** with parallel UI work starting after Phase 2

\* Phase 5 can start in parallel with Phases 3-4 using Phase 2 API

---

## Open Questions

1. **Codec Support:** Should we support HEVC (H.265) from the start, or add later?
2. **Container Format:** Should MKV be primary (better multi-track) or MP4 (better compatibility)?
3. **Audio Processing:** Should we add real-time effects (noise suppression, EQ, compression)?
4. **Streaming:** Should this architecture also support streaming to RTMP/SRT?
5. **GPU Encoding:** Which GPU APIs should we prioritize (NVENC, QuickSync, AMD VCE)?
6. **Plugin System:** Should we support third-party sources via plugins (like OBS)?

---

## Conclusion

This multi-phase plan provides a clear path to implementing OBS-style capture architecture in CaptureTool. The phased approach:
- Minimizes risk by building on existing working code
- Allows testing and validation at each stage
- Enables parallel development of backend and UI
- Provides clear success criteria for each phase
- Maintains backward compatibility throughout

The plan is ambitious but achievable over ~6 months with focused development. Each phase delivers incremental value and can be shipped independently if needed.

**Next Steps:**
1. Review and approval of this plan
2. Create detailed task breakdown for Phase 1
3. Set up feature branches and development workflow
4. Begin Phase 1 implementation

---

## References

- [OBS Studio Backend Design](https://docs.obsproject.com/backend-design)
- [OBS Studio Source System](https://deepwiki.com/streamlabs/obs-studio/2.1-source-system)
- [Windows Media Foundation Documentation](https://docs.microsoft.com/en-us/windows/win32/medfound/microsoft-media-foundation-sdk)
- [WASAPI Audio Capture](https://docs.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream)
- [Windows.Graphics.Capture API](https://docs.microsoft.com/en-us/uwp/api/windows.graphics.capture)

---

**Document Version:** 1.0  
**Last Updated:** 2025-12-18  
**Author:** GitHub Copilot (CaptureTool Planning Session)
