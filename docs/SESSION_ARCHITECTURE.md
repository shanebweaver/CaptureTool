# Capture Session Architecture

## Overview

The capture session implements screen recording using Windows Graphics Capture API with hardware-accelerated video encoding. The architecture follows Clean Architecture principles with explicit ownership semantics and RAII resource management.

## Core Components

### WindowsGraphicsCaptureSession
Central orchestrator that coordinates audio/video capture with the media sink.

**Responsibilities:**
- Initialize and coordinate capture sources (audio/video)
- Manage media clock for A/V synchronization
- Route captured data to sink writer
- Handle session lifecycle (start/stop/pause/resume)

**Dependencies (injected via constructor):**
- `IMediaClock` - Timeline management and A/V sync
- `IAudioCaptureSource` - Audio capture from system
- `IVideoCaptureSource` - Video capture via Windows.Graphics.Capture
- `IMP4SinkWriter` - MP4 encoding and file output

### Capture Sources
**Audio Source:** Captures system audio, acts as clock advancer to drive the media timeline.

**Video Source:** Captures screen content using hardware-accelerated Windows.Graphics.Capture API.

### Media Clock
Provides synchronized timeline for audio and video streams. Audio source advances the clock to ensure proper A/V sync.

### Sink Writer
Encodes captured audio/video to MP4 format using Media Foundation and writes to file.

## Architectural Patterns

### Dependency Injection
All dependencies are provided via constructor, establishing clear ownership from the start. The factory creates and wires all components before returning the session.

### RAII (Resource Acquisition Is Initialization)
All resources use automatic management:
- `std::unique_ptr` for owned dependencies
- `std::mutex` for thread synchronization (no manual Init/Delete)
- Destructors handle cleanup automatically

### Separation of Concerns
- **Factory:** Creates and connects dependencies
- **Session:** Orchestrates capture workflow
- **Sources:** Handle platform-specific capture
- **Sink:** Manages encoding and output

### State Management
- `m_isInitialized` - Initialization complete
- `m_isActive` - Capture running
- `m_isShuttingDown` - Atomic flag for thread-safe shutdown

Initialization is separated from starting, preventing partially-initialized states.

### Thread Safety
- Atomic shutdown flag prevents callback races during Stop()
- Mutex protects callback function pointers
- Lock guards ensure exception-safe lock management

## Lifecycle

```
Factory.CreateSession()
  └─> Create Dependencies (clock, sources, sink)
  └─> Construct Session (inject dependencies)
  └─> Initialize Session (setup sources & callbacks)
  └─> Return Session (or nullptr on failure)

Session.Start()
  └─> Start Clock
  └─> Start Audio Capture
  └─> Start Video Capture
  └─> Capture active

Session.Stop()
  └─> Set Shutdown Flag
  └─> Stop Sources
  └─> Clear Callbacks
  └─> Finalize Sink Writer
```

## Data Flow

```
Audio Source → Audio Samples → Sink Writer → MP4 File
                      ↓
                 User Callback
                 
Video Source → Video Frames → Sink Writer → MP4 File
                      ↓
                 User Callback
                 
Audio Source → Clock Advancer → Media Clock → Timeline
```

## Shutdown Sequence

The Stop() method follows a carefully ordered sequence to ensure thread safety:

1. **Set shutdown flag** (atomic) - Callbacks check this and abort
2. **Stop sources** - No new data generated, in-flight callbacks complete
3. **Clear callbacks** - Safe because sources stopped
4. **Finalize resources** - Wait for encoder queue drain, close file

This ordering ensures no use-after-free and no dangling callbacks.

## Key Design Decisions

### Explicit Ownership
Dependencies use `std::unique_ptr` rather than raw pointers or shared ownership, making lifetimes explicit and eliminating dangling pointer risks.

### Fail-Fast Validation
Configuration is validated at boundaries (factory, Initialize, Start) with early failure rather than partial initialization.

### Callback Lifetime Contract
Callbacks guaranteed not to execute after Stop() returns. Shutdown flag and source stopping ensure this invariant.

### Value Types
`CaptureSessionConfig` owns its data (`std::wstring` for paths) rather than borrowing via raw pointers, enabling safe copying and clear lifetime semantics.

## Future Considerations

- Configurable encoder settings (bitrate, framerate)
- Error recovery and retry logic
- Performance monitoring and diagnostics
- Support for additional capture sources
