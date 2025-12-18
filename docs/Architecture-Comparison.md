# Architecture Comparison: Current vs. OBS-Style

## Current Architecture (Monolithic)

```
┌─────────────────────────────────────────────────────────────┐
│                     CaptureTool (Current)                   │
└─────────────────────────────────────────────────────────────┘

User Interface (C# WinUI)
     │
     ├─── Start Recording ────────────────┐
     │                                     ▼
     │                            ┌────────────────┐
     │                            │ ScreenRecorder │
     │                            │   (C++ DLL)    │
     │                            └────────┬───────┘
     │                                     │
     ├─── Toggle Desktop Audio ───────────┤
     │                                     │
     └─── Stop Recording ─────────────────┤
                                          │
                                          ▼
                    ┌──────────────────────────────────────┐
                    │         MP4SinkWriter                │
                    │  (Tightly Coupled Muxer)             │
                    ├──────────────────────────────────────┤
                    │  Video: H.264 Encoder                │
                    │  Audio: AAC Encoder (Desktop Only)   │
                    │  Muxing: Single audio track          │
                    └──────────────┬───────────────────────┘
                                   │
                                   ▼
                          ┌─────────────────┐
                          │  output.mp4     │
                          │  - Video        │
                          │  - Audio (1trk) │
                          └─────────────────┘

Limitations:
❌ Single audio source (desktop loopback only)
❌ No mic support during screen recording
❌ No per-source volume control
❌ Cannot record separate audio tracks
❌ All-or-nothing audio recording
```

---

## Target Architecture (OBS-Style, Modular)

```
┌─────────────────────────────────────────────────────────────┐
│              CaptureTool (OBS-Style Architecture)           │
└─────────────────────────────────────────────────────────────┘

User Interface (C# WinUI)
     │
     ├─── Source Management ──────────┐
     │    (Add/Remove/Configure)       │
     │                                 ▼
     │                    ┌──────────────────────┐
     │                    │   Source Registry    │
     │                    └─────────┬────────────┘
     │                              │
     ├─── Audio Routing ────────────┤
     │    (Track Assignment)         │
     │                              ▼
     │              ┌───────────────────────────────────┐
     │              │      Source Coordinator           │
     │              └───┬───────────────────────────┬───┘
     │                  │                           │
     │                  ▼                           ▼
     │      ┌─────────────────────┐    ┌──────────────────────┐
     │      │   Video Sources     │    │   Audio Sources      │
     │      ├─────────────────────┤    ├──────────────────────┤
     │      │ • Screen Capture    │    │ • Desktop Audio      │
     │      │ • Window Capture    │    │ • Microphone         │
     │      │ • Camera (Future)   │    │ • Per-App Audio      │
     │      └──────────┬──────────┘    └──────────┬───────────┘
     │                 │                           │
     │                 │                           ▼
     │                 │              ┌──────────────────────┐
     │                 │              │    Audio Mixer       │
     │                 │              ├──────────────────────┤
     │                 │              │ • Per-source volume  │
     │                 │              │ • Mute control       │
     │                 │              │ • Track routing      │
     │                 │              │ • Sample rate conv.  │
     │                 │              └──────────┬───────────┘
     │                 │                         │
     │                 ▼                         ▼
     │       ┌──────────────────┐    ┌──────────────────────┐
     │       │  Video Encoder   │    │  Audio Encoders      │
     │       ├──────────────────┤    ├──────────────────────┤
     │       │ • H.264          │    │ Track 1: Desktop     │
     │       │ • H.265 (future) │    │ Track 2: Microphone  │
     │       │ • Hardware accel │    │ Track 3: App Audio   │
     │       └────────┬─────────┘    └──────────┬───────────┘
     │                │                          │
     │                └──────────┬───────────────┘
     │                           │
     │                           ▼
     │              ┌──────────────────────────┐
     │              │    Interleaver/Muxer     │
     │              ├──────────────────────────┤
     │              │ • Multi-track support    │
     │              │ • MP4 / MKV containers   │
     │              │ • Proper A/V interleave  │
     │              └──────────┬───────────────┘
     │                         │
     └─── Per-Source Control ──┤
          (Volume, Mute, etc)  │
                               ▼
                      ┌──────────────────┐
                      │   output.mp4     │
                      │   - Video        │
                      │   - Audio Track 1│
                      │   - Audio Track 2│
                      │   - Audio Track 3│
                      └──────────────────┘

Benefits:
✅ Multiple audio sources simultaneously
✅ Independent volume/mute per source
✅ Multi-track recording (up to 6 tracks)
✅ Flexible routing (any source → any track)
✅ Hot-swap sources during recording
✅ Professional post-production workflow
```

---

## Key Architecture Changes

### 1. Source Abstraction

**Before:**
```cpp
// Monolithic, tightly coupled
TryStartRecording(hMonitor, outputPath, captureAudio);
```

**After:**
```cpp
// Flexible, source-based
void* screenSource = CreateVideoSource(screenConfig);
void* desktopAudio = CreateAudioSource(desktopConfig);
void* micAudio = CreateAudioSource(micConfig);

StartRecordingWithSources(outputPath, 
    {screenSource}, 1,           // video sources
    {desktopAudio, micAudio}, 2  // audio sources
);
```

### 2. Audio Pipeline

**Before:**
```
Desktop Audio → AudioCaptureHandler → MP4SinkWriter → File
                (single source)      (single track)
```

**After:**
```
Desktop Audio ──┐
Microphone ─────┼→ AudioMixer → Track Routing → Encoders → Muxer → File
App Audio 1 ────┤   (per-source    (assign to      (AAC)
App Audio 2 ────┘    controls)      tracks 1-6)
```

### 3. Control Surface

**Before:**
```csharp
interface IVideoCaptureHandler {
    void StartVideoCapture(args);
    void StopVideoCapture();
    void ToggleDesktopAudioCapture(bool enabled);  // Single toggle
}
```

**After:**
```csharp
interface ISourceRegistry {
    // Multiple sources
    Task<IVideoSource> CreateScreenCaptureSourceAsync(...);
    Task<IAudioSource> CreateDesktopAudioSourceAsync();
    Task<IAudioSource> CreateMicrophoneSourceAsync(deviceId);
    void RemoveSource(source);
}

interface IAudioSource {
    float Volume { get; set; }      // Per-source control
    bool IsMuted { get; set; }      // Per-source mute
}

interface IAudioRoutingService {
    void AssignSourceToTrack(source, trackIndex);  // Flexible routing
}
```

---

## Data Flow Comparison

### Current: Monolithic Flow

```
[User clicks Record]
    ↓
[ScreenRecorder::TryStartRecording]
    ↓
[Initialize MP4SinkWriter] ← Video + Audio together
    ↓
[Start Graphics.Capture] → FrameArrivedHandler → WriteFrame()
    ↓
[Start AudioCaptureHandler] → WriteAudioSample() (single track)
    ↓
[Single MP4 file with 1 audio track]
```

### Target: Modular Flow

```
[User configures sources in UI]
    ↓
[Source Registry manages source lifecycle]
    ↓
[User configures audio routing]
    ↓
[User clicks Record]
    ↓
[Source Coordinator starts all active sources]
    ↓
┌──────────────────┐                 ┌──────────────────┐
│ Video Sources    │                 │ Audio Sources    │
│ • Each calls     │                 │ • Each calls     │
│   OnFrame()      │                 │   OnAudioData()  │
└────────┬─────────┘                 └────────┬─────────┘
         │                                    │
         ▼                                    ▼
   [Video Encoder]                    [Audio Mixer]
         │                             • Combines sources
         │                             • Applies volumes
         │                             • Routes to tracks
         │                                    │
         └──────────────┬─────────────────────┘
                        ▼
                [Interleaver/Muxer]
                • Synchronizes streams
                • Writes multi-track file
                        ↓
           [MP4 file with N audio tracks]
```

---

## Synchronization Strategy

### Current: Shared Start Time

```cpp
// All capture shares one start time
LONGLONG m_recordingStartQpc = 0;

// Video sets start time
SetRecordingStartTime(qpcNow);

// Audio uses same start time
LONGLONG relativeTime = (qpcNow - m_startQpc) * 10000000 / qpcFrequency;
```

### Target: Reference Clock System

```cpp
// Central reference clock
class ReferenceClock {
    LONGLONG m_recordingStartQpc;
    LARGE_INTEGER m_qpcFrequency;
    
public:
    void Start() { QueryPerformanceCounter(&m_recordingStartQpc); }
    LONGLONG GetRelativeTime();  // All sources use this
};

// Each source syncs to reference
ReferenceClock& clock = ReferenceClock::Instance();
LONGLONG timestamp = clock.GetRelativeTime();

// Handles pause/resume
clock.Pause();   // Freezes time progression
clock.Resume();  // Resumes with adjusted offset
```

---

## Implementation Phases Timeline

```
Phase 1: Source Abstraction
├─ Week 1-2: Define interfaces, refactor existing code
└─ Week 3: Testing, API updates
   ✓ Deliverable: Existing features work with new architecture

Phase 2: Multiple Sources
├─ Week 4-5: Microphone source, device enumeration
├─ Week 6-7: Per-app audio capture (Windows 11)
└─ Week 7: Source registry and management
   ✓ Deliverable: Can record desktop + mic simultaneously

Phase 3: Audio Mixer
├─ Week 8-10: Audio mixer core, volume control
├─ Week 11: Multi-track MP4SinkWriter
└─ Week 12: Routing configuration
   ✓ Deliverable: Multi-track recording with routing

Phase 4: Advanced Muxing
├─ Week 13-15: Separate encoding pipeline
├─ Week 16-17: Advanced interleaving, codec options
└─ Week 18: Configuration APIs
   ✓ Deliverable: Flexible encoder/muxer control

Phase 5: UI (Parallel with 3-4)
├─ Week 8-11: Source management UI
├─ Week 12-15: Audio routing UI
├─ Week 16-19: Recording config UI
└─ Week 20-23: Live monitoring, polish
   ✓ Deliverable: Intuitive source/routing UI

Testing & Polish
└─ Week 24-26: Integration testing, bug fixes, optimization
   ✓ Deliverable: Production-ready release
```

---

## Success Criteria Comparison

| Feature | Current | After Phase 1 | After Phase 2 | After Phase 3 | Final |
|---------|---------|---------------|---------------|---------------|-------|
| Screen capture | ✅ | ✅ | ✅ | ✅ | ✅ |
| Desktop audio | ✅ | ✅ | ✅ | ✅ | ✅ |
| Microphone | ❌ | ❌ | ✅ | ✅ | ✅ |
| Per-app audio | ❌ | ❌ | ✅ | ✅ | ✅ |
| Multiple audio sources | ❌ | ❌ | ✅ | ✅ | ✅ |
| Multi-track recording | ❌ | ❌ | ❌ | ✅ | ✅ |
| Per-source volume | ❌ | ❌ | ❌ | ✅ | ✅ |
| Audio routing | ❌ | ❌ | ❌ | ✅ | ✅ |
| Encoder options | ❌ | ❌ | ❌ | ❌ | ✅ |
| Source management UI | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## Risk Mitigation

### Phase 1 Risk: Breaking Existing Features
**Mitigation:** Maintain existing API, add comprehensive tests, gradual refactoring

### Phase 2-3 Risk: Audio Synchronization
**Mitigation:** Reference clock design, extensive timing tests, QPC-based sync

### Phase 4 Risk: Encoding Pipeline Complexity
**Mitigation:** Incremental encoder separation, fallback to simpler paths, performance profiling

### Phase 5 Risk: UI Complexity
**Mitigation:** User testing, iterative design, preset system for common scenarios

---

_This architecture comparison illustrates the transformation from a monolithic capture system to a flexible, OBS-style modular architecture._
