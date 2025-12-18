# CaptureInterop - Source Abstraction Architecture

## Overview

CaptureInterop is the native C++ component that handles screen and audio capture for CaptureTool. As of Phase 1, the capture logic has been refactored using a source-based architecture.

## Architecture

### Phase 1: Source Abstraction Layer (Complete)

Phase 1 establishes the foundation for modular capture sources while maintaining 100% backward compatibility with the existing API.

### Source Abstraction Interfaces

#### Base Interface: IMediaSource

Located in `IMediaSource.h`, this is the foundation for all capture sources.

**Key Methods:**
- `Initialize()` - Set up resources before starting capture
- `Start()` - Begin capturing
- `Stop()` - Stop capturing (idempotent, can be called multiple times)
- `IsRunning()` - Check capture status
- `AddRef() / Release()` - COM-style reference counting for lifetime management

#### Video Sources: IVideoSource

Located in `IVideoSource.h`, extends IMediaSource for video capture.

**Key Methods:**
- `GetResolution(width, height)` - Query captured frame dimensions
- `SetFrameCallback(callback)` - Register callback to receive frames

**Callback Signature:**
```cpp
using VideoFrameCallback = std::function<void(ID3D11Texture2D* texture, LONGLONG timestamp)>;
```

#### Audio Sources: IAudioSource

Located in `IAudioSource.h`, extends IMediaSource for audio capture.

**Key Methods:**
- `GetFormat()` - Query audio format (WAVEFORMATEX)
- `SetAudioCallback(callback)` - Register callback to receive audio samples
- `SetEnabled(bool)` - Runtime mute/unmute control
- `IsEnabled()` - Query mute status

**Callback Signature:**
```cpp
using AudioSampleCallback = std::function<void(const BYTE* data, UINT32 numFrames, LONGLONG timestamp)>;
```

### Implemented Sources

#### ScreenCaptureSource

**Location:** `ScreenCaptureSource.h/cpp`

**Purpose:** Captures screen content using Windows.Graphics.Capture API.

**Key Features:**
- Encapsulates all Graphics.Capture session management
- Handles D3D11 device integration
- Provides callback-based frame delivery
- Reference-counted lifecycle management

**Usage Pattern:**
```cpp
auto source = new ScreenCaptureSource();
source->SetMonitor(hMonitor);
source->SetDevice(d3d11Device);
source->Initialize();
source->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG ts) {
    // Process frame
});
source->Start();
// ... recording ...
source->Stop();
source->Release();
```

#### DesktopAudioSource

**Location:** `DesktopAudioSource.h/cpp`

**Purpose:** Captures desktop audio (loopback) using WASAPI.

**Key Features:**
- Encapsulates WASAPI loopback capture
- Maintains timestamp accumulation (prevents audio speedup)
- Supports runtime enable/disable (muting)
- Dedicated ABOVE_NORMAL priority capture thread
- Reference-counted lifecycle management

**Usage Pattern:**
```cpp
auto source = new DesktopAudioSource();
source->Initialize();
source->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
    // Process audio
});
source->Start();
// ... recording ...
source->SetEnabled(false);  // Mute
source->SetEnabled(true);   // Unmute
source->Stop();
source->Release();
```

### Supporting Components

#### FrameArrivedHandler

**Location:** `FrameArrivedHandler.h/cpp`

**Purpose:** Handles Windows.Graphics.Capture frame events and forwards them via callback or to MP4SinkWriter.

**Phase 1 Changes:**
- Added callback-based constructor
- Maintained legacy MP4SinkWriter constructor for backward compatibility
- Dual-path ProcessingThreadProc supports both modes

**Usage (New Callback-Based):**
```cpp
auto handler = new FrameArrivedHandler([](ID3D11Texture2D* tex, LONGLONG ts) {
    // Handle frame
});
auto token = framePool->add_FrameArrived(handler, &token);
handler->StartProcessing();
```

**Usage (Legacy):**
```cpp
auto handler = new FrameArrivedHandler(sinkWriter);
auto token = framePool->add_FrameArrived(handler, &token);
handler->StartProcessing();
```

#### MP4SinkWriter

**Location:** `MP4SinkWriter.h/cpp`

**No Changes in Phase 1** - Already source-agnostic. Accepts frames and audio samples via `WriteFrame()` and `WriteAudioSample()` methods.

#### AudioCaptureDevice

**Location:** `AudioCaptureDevice.h/cpp`

**No Changes in Phase 1** - Low-level WASAPI wrapper, reused by DesktopAudioSource.

### Current Data Flow

#### With Source Abstraction (Phase 1 Ready, Not Yet Active):
```
User → TryStartRecording()
    ↓
[Create ScreenCaptureSource]
    ↓
[Create DesktopAudioSource (if audio enabled)]
    ↓
[Initialize MP4SinkWriter]
    ↓
[Register callbacks: source → MP4SinkWriter]
    ↓
[Start all sources]

During Recording:
    ScreenCaptureSource → callback → MP4SinkWriter::WriteFrame()
    DesktopAudioSource  → callback → MP4SinkWriter::WriteAudioSample()
```

#### Current Active Path (Backward Compatible):
```
User → TryStartRecording()
    ↓
[Initialize D3D11, Graphics.Capture session (direct)]
    ↓
[Initialize MP4SinkWriter]
    ↓
[Initialize AudioCaptureHandler (if audio)]
    ↓
[RegisterFrameArrivedHandler(framePool, sinkWriter)]
    ↓
[Start capture]

During Recording:
    Graphics.Capture → FrameArrivedHandler → MP4SinkWriter::WriteFrame()
    AudioCaptureHandler → MP4SinkWriter::WriteAudioSample()
```

Both paths work correctly due to dual-path design in FrameArrivedHandler.

## Phase 1 Achievements

✅ **Clean Abstractions Created:**
- IMediaSource, IVideoSource, IAudioSource interfaces
- ScreenCaptureSource implementation
- DesktopAudioSource implementation

✅ **Backward Compatibility Maintained:**
- All existing exports work unchanged
- FrameArrivedHandler supports both legacy and callback-based modes
- Same performance and output quality

✅ **Foundation for Phase 2:**
- Pattern established for adding sources
- Callback mechanism implemented and tested
- Lifecycle management proven
- Ready to add MicrophoneAudioSource, ApplicationAudioSource

## Next Steps: Phase 2

### Planned Additions

1. **MicrophoneAudioSource**
   - WASAPI capture endpoint (not loopback)
   - Device enumeration and selection
   - Independent capture from desktop audio

2. **ApplicationAudioSource**
   - Per-process audio via Audio Session API
   - Windows 11 22H2+ required
   - Process lifecycle tracking

3. **SourceManager**
   - Coordinate multiple sources
   - Unified start/stop
   - Source registration and discovery

4. **C# Integration**
   - Expose source enumeration to C# layer
   - Update ViewModels to support multiple sources
   - UI for source selection (Phase 5)

### Migration Path

Currently, ScreenRecorder uses the legacy path for stability. To migrate:

1. Replace direct Graphics.Capture calls with ScreenCaptureSource
2. Replace AudioCaptureHandler with DesktopAudioSource
3. Use callbacks to bridge sources to MP4SinkWriter
4. Update global state management

Example:
```cpp
static ScreenCaptureSource* g_videoSource = nullptr;
static DesktopAudioSource* g_audioSource = nullptr;

bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio)
{
    // Initialize D3D11
    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    
    // Create video source
    g_videoSource = new ScreenCaptureSource();
    g_videoSource->SetMonitor(hMonitor);
    g_videoSource->SetDevice(d3d.device.get());
    g_videoSource->Initialize();
    
    // Get resolution
    UINT32 width, height;
    g_videoSource->GetResolution(width, height);
    
    // Initialize sink writer
    g_sinkWriter.Initialize(outputPath, d3d.device.get(), width, height, &hr);
    
    // Set up video callback
    g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
        g_sinkWriter.WriteFrame(texture, timestamp);
    });
    
    // Create audio source if requested
    if (captureAudio)
    {
        g_audioSource = new DesktopAudioSource();
        g_audioSource->Initialize();
        
        WAVEFORMATEX* format = g_audioSource->GetFormat();
        g_sinkWriter.InitializeAudioStream(format, &hr);
        
        g_audioSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts) {
            g_sinkWriter.WriteAudioSample(data, frames, ts);
        });
        
        g_audioSource->Start();
    }
    
    // Start video
    g_videoSource->Start();
    
    return true;
}
```

## File Organization

### Source Abstraction Filter
- `IMediaSource.h` - Base interface
- `IVideoSource.h` - Video source interface
- `IAudioSource.h` - Audio source interface
- `ScreenCaptureSource.h/cpp` - Screen capture implementation
- `DesktopAudioSource.h/cpp` - Desktop audio implementation

### Core Components
- `ScreenRecorder.h/cpp` - Global coordinator (to be refactored in Phase 2)
- `MP4SinkWriter.h/cpp` - Encoder and muxer
- `FrameArrivedHandler.h/cpp` - Frame event handler with dual-path support
- `AudioCaptureDevice.h/cpp` - Low-level WASAPI wrapper
- `AudioCaptureHandler.h/cpp` - Legacy audio handler (to be deprecated in Phase 2)

## Testing Checklist

✅ **Phase 1 Acceptance Criteria:**
- [x] All interfaces compile without errors
- [x] ScreenCaptureSource encapsulates video capture
- [x] DesktopAudioSource encapsulates audio capture
- [x] FrameArrivedHandler supports callbacks
- [x] Backward compatibility maintained
- [x] Project files updated
- [x] Documentation created

## Known Issues

None. Phase 1 complete and stable.

## References

- [Phase 1 Development Plan](../../../../docs/Phase-1-Development-Plan.md)
- [OBS-Style Capture Architecture Plan](../../../../docs/OBS-Style-Capture-Architecture-Plan.md)
- [Architecture Comparison](../../../../docs/Architecture-Comparison.md)

---

**Document Version:** 1.0  
**Last Updated:** 2025-12-18  
**Phase 1 Status:** Complete
