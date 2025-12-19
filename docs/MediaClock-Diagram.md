# MediaClock Architecture Diagrams

This document contains visual representations of the MediaClock architecture using Mermaid diagrams (renderable in GitHub and many markdown viewers).

## Current Architecture (Before MediaClock)

```mermaid
graph TD
    A[ScreenRecorderImpl] --> B[AudioCaptureHandler]
    A --> C[FrameArrivedHandler]
    B --> D[MP4SinkWriter]
    C --> D
    
    B -.-> E[Timing State:<br/>m_startQpc<br/>m_qpcFrequency<br/>m_nextAudioTimestamp]
    C -.-> F[Timing State:<br/>m_firstFrameSystemTime]
    D -.-> G[Sync Point:<br/>m_recordingStartQpc<br/>First-to-start wins]
    
    style E fill:#ffcccc
    style F fill:#ffcccc
    style G fill:#ffffcc
    
    classDef problem fill:#ffcccc,stroke:#ff0000,stroke-width:2px
    classDef sync fill:#ffffcc,stroke:#ffaa00,stroke-width:2px
```

**Issues**:
- 🔴 Timing state scattered across multiple components
- 🟡 Race condition: whichever starts first sets m_recordingStartQpc
- 🔴 MP4SinkWriter has dual role (sink + coordinator)

## Proposed Architecture (With MediaClock)

```mermaid
graph TD
    A[ScreenRecorderImpl] --> B[AudioCaptureHandler]
    A --> C[FrameArrivedHandler]
    A --> D[MP4SinkWriter]
    B --> D
    C --> D
    
    D --> E[MediaClock]
    B -.queries.-> E
    C -.queries.-> E
    
    E -.-> F[Timing State:<br/>m_startQpc<br/>m_qpcFrequency<br/>m_isStarted<br/><br/>Thread-safe<br/>Single source of truth]
    
    B -.-> G[Only needs:<br/>m_nextAudioTimestamp<br/>accumulated timestamp]
    C -.-> H[No timing state needed!<br/>Just queries clock]
    
    style E fill:#ccffcc,stroke:#00aa00,stroke-width:3px
    style F fill:#ccffcc
    style G fill:#e6f3ff
    style H fill:#e6f3ff
    
    classDef good fill:#ccffcc,stroke:#00aa00,stroke-width:2px
    classDef clean fill:#e6f3ff,stroke:#0066cc,stroke-width:2px
```

**Improvements**:
- ✅ Single source of truth for timing
- ✅ Clear ownership: MP4SinkWriter owns clock
- ✅ No race conditions: clock started explicitly
- ✅ Consumers only query, don't modify timing state

## Component Interaction Sequence

```mermaid
sequenceDiagram
    participant SR as ScreenRecorderImpl
    participant MS as MP4SinkWriter
    participant MC as MediaClock
    participant AH as AudioCaptureHandler
    participant FH as FrameArrivedHandler
    
    SR->>MS: Initialize(outputPath, device, width, height)
    MS->>MC: Create MediaClock (member)
    SR->>MC: Start() - Initialize time base
    MC-->>SR: Started
    
    SR->>AH: Start()
    AH->>MS: GetClock()
    MS-->>AH: clock pointer
    
    SR->>FH: RegisterFrameArrivedHandler()
    FH->>MS: GetClock()
    MS-->>FH: clock pointer
    
    SR->>SR: StartCapture()
    
    loop Recording
        AH->>MC: GetElapsedTime()
        MC-->>AH: timestamp (e.g., 50ms)
        AH->>MS: WriteAudioSample(data, timestamp)
        
        FH->>MC: GetElapsedTime()
        MC-->>FH: timestamp (e.g., 53ms)
        FH->>MS: WriteFrame(texture, timestamp)
    end
    
    SR->>AH: Stop()
    SR->>FH: Stop()
    SR->>MS: Finalize()
    MS->>MC: Reset()
```

## Timing Flow Visualization

```mermaid
gantt
    title Recording Timeline with MediaClock
    dateFormat X
    axisFormat %L ms
    
    section Clock
    Start Clock :milestone, m1, 0, 0ms
    GetElapsedTime (50ms) :milestone, m2, 50, 0ms
    GetElapsedTime (100ms) :milestone, m3, 100, 0ms
    GetElapsedTime (150ms) :milestone, m4, 150, 0ms
    
    section Audio
    Audio Start :active, a1, 0, 10ms
    Audio Sample 1 (ts=50ms) :active, a2, 50, 10ms
    Audio Sample 2 (ts=100ms) :active, a3, 100, 10ms
    Audio Sample 3 (ts=150ms) :active, a4, 150, 10ms
    
    section Video
    Video Start :crit, v1, 5, 10ms
    Video Frame 1 (ts=50ms) :crit, v2, 50, 33ms
    Video Frame 2 (ts=100ms) :crit, v3, 100, 33ms
    Video Frame 3 (ts=150ms) :crit, v4, 150, 33ms
```

**Key Points**:
- Clock provides consistent time base
- Audio samples every ~10ms (WASAPI buffer size)
- Video frames every ~33ms (30 fps)
- All timestamps reference same clock → perfect sync

## Class Structure

```mermaid
classDiagram
    class MediaClock {
        -LONGLONG m_startQpc
        -LARGE_INTEGER m_qpcFrequency
        -mutex m_mutex
        -atomic~bool~ m_isStarted
        +MediaClock()
        +bool Start()
        +void Reset()
        +bool IsStarted()
        +LONGLONG GetElapsedTime()
        +LONGLONG GetStartQpc()
        +void Pause()* future
        +void Resume()* future
    }
    
    class MP4SinkWriter {
        -MediaClock m_clock
        -IMFSinkWriter* m_sinkWriter
        -DWORD m_videoStreamIndex
        -DWORD m_audioStreamIndex
        +bool Initialize(path, device, width, height)
        +bool InitializeAudioStream(format)
        +MediaClock* GetClock()
        +HRESULT WriteFrame(texture, timestamp)
        +HRESULT WriteAudioSample(data, frames, timestamp)
        +void Finalize()
    }
    
    class AudioCaptureHandler {
        -MP4SinkWriter* m_sinkWriter
        -LONGLONG m_nextAudioTimestamp
        +bool Start()
        +void Stop()
        +void SetSinkWriter(writer)
        -void CaptureThreadProc()
    }
    
    class FrameArrivedHandler {
        -MP4SinkWriter* m_sinkWriter
        -atomic~bool~ m_clockStarted
        +HRESULT Invoke(sender, args)
        +void StartProcessing()
        +void Stop()
    }
    
    class ScreenRecorderImpl {
        -MP4SinkWriter m_sinkWriter
        -AudioCaptureHandler* m_audioHandler
        -FrameArrivedHandler* m_frameHandler
        +bool StartRecording(monitor, path, captureAudio)
        +void StopRecording()
        +void PauseRecording()
        +void ResumeRecording()
    }
    
    MP4SinkWriter *-- MediaClock : owns
    AudioCaptureHandler --> MP4SinkWriter : uses
    FrameArrivedHandler --> MP4SinkWriter : uses
    ScreenRecorderImpl *-- MP4SinkWriter : owns
    ScreenRecorderImpl *-- AudioCaptureHandler : owns
    ScreenRecorderImpl --> FrameArrivedHandler : creates
    
    AudioCaptureHandler ..> MediaClock : queries via\nGetClock()
    FrameArrivedHandler ..> MediaClock : queries via\nGetClock()
```

## State Machine

```mermaid
stateDiagram-v2
    [*] --> NotStarted : MediaClock created
    NotStarted --> Started : Start()
    Started --> Started : GetElapsedTime()
    Started --> NotStarted : Reset()
    
    note right of NotStarted
        m_isStarted = false
        GetElapsedTime() returns 0
    end note
    
    note right of Started
        m_isStarted = true
        m_startQpc != 0
        GetElapsedTime() returns elapsed ticks
    end note
    
    Started --> Paused : Pause()
    Paused --> Started : Resume()
    
    note right of Paused
        Future feature
        Time freezes at pause point
    end note
```

## Thread Safety Model

```mermaid
graph LR
    subgraph "Lock-Free Read Path (Hot Path)"
        A[GetElapsedTime] --> B{IsStarted?}
        B -->|No| C[Return 0]
        B -->|Yes| D[QueryPerformanceCounter]
        D --> E[Calculate: elapsed * 10M / freq]
        E --> F[Return ticks]
    end
    
    subgraph "Locked Write Path (Cold Path)"
        G[Start] --> H[Acquire Mutex]
        H --> I{Already Started?}
        I -->|Yes| J[Release Mutex<br/>Return false]
        I -->|No| K[QueryPerformanceCounter]
        K --> L[Store m_startQpc]
        L --> M[Set m_isStarted = true]
        M --> N[Release Mutex<br/>Return true]
    end
    
    style A fill:#ccffcc
    style D fill:#ccffcc
    style E fill:#ccffcc
    style F fill:#ccffcc
    
    style G fill:#ffffcc
    style H fill:#ffcccc
    style N fill:#ffffcc
```

**Key Points**:
- Green path (reads): No locks, minimal overhead
- Yellow path (writes): Locked, but rare (once per session)
- Red section: Critical section protected by mutex

## Migration Phases

```mermaid
gantt
    title MediaClock Implementation Phases
    dateFormat YYYY-MM-DD
    section Phase 1
    Create MediaClock class :p1, 2025-01-01, 3d
    Write unit tests :p1t, after p1, 2d
    
    section Phase 2
    Integrate with MP4SinkWriter :p2, after p1t, 2d
    Test backward compat :p2t, after p2, 1d
    
    section Phase 3
    Migrate AudioCaptureHandler :p3, after p2t, 3d
    Test audio recording :p3t, after p3, 2d
    
    section Phase 4
    Migrate FrameArrivedHandler :p4, after p3t, 3d
    Test video recording :p4t, after p4, 2d
    
    section Phase 5
    Update ScreenRecorderImpl :p5, after p4t, 2d
    Integration tests :p5t, after p5, 2d
    
    section Phase 6
    Remove old sync code :p6, after p5t, 2d
    Final regression tests :p6t, after p6, 3d
```

## Performance Comparison

```mermaid
graph LR
    subgraph "Current Implementation"
        A1[Audio: QPC + local calc] --> C1[Audio Timestamp]
        A2[Video: System time + local calc] --> C2[Video Timestamp]
        A3[Sync: First-to-start race] --> C3[May drift]
    end
    
    subgraph "With MediaClock"
        B1[Audio: GetElapsedTime] --> D1[Audio Timestamp]
        B2[Video: GetElapsedTime] --> D2[Video Timestamp]
        B3[Clock: Single time base] --> D3[Always synced]
    end
    
    style C3 fill:#ffcccc
    style D3 fill:#ccffcc
```

**Metrics**:
- Current: Each pipeline does QPC + conversion independently
- MediaClock: Each pipeline calls `GetElapsedTime()` (same overhead)
- Net difference: ~1 pointer dereference + 1 atomic load ≈ **< 1% overhead**

## Future Architecture Vision

```mermaid
graph TD
    A[ScreenRecorderImpl] --> MC[MediaClock]
    
    A --> B[Audio Source<br/>System Loopback]
    A --> C[Audio Source<br/>Microphone]
    A --> D[Video Source<br/>Screen Capture]
    A --> E[Video Source<br/>Webcam]
    
    B --> F[Muxer/Encoder]
    C --> F
    D --> F
    E --> F
    
    MC -.time.-> B
    MC -.time.-> C
    MC -.time.-> D
    MC -.time.-> E
    
    F --> G[MP4 Output]
    F --> H[Streaming Output]
    
    style MC fill:#ccffcc,stroke:#00aa00,stroke-width:3px
    style F fill:#e6f3ff,stroke:#0066cc,stroke-width:2px
```

**Future Benefits**:
- Multiple independent sources
- Flexible routing (mux any combination)
- Multiple output formats simultaneously
- All synchronized via shared MediaClock

---

*These diagrams can be rendered in GitHub markdown viewers, VS Code with Mermaid extension, and many other tools.*
