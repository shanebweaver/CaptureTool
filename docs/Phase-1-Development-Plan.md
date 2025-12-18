# Phase 1: Source Abstraction Layer - Detailed Development Plan

**Phase Duration:** 2-3 weeks  
**Goal:** Create a flexible source abstraction that decouples capture sources from the muxer while maintaining 100% backward compatibility with existing functionality.

---

## Table of Contents

1. [Overview](#overview)
2. [Current Architecture Deep Dive](#current-architecture-deep-dive)
3. [Target Architecture](#target-architecture)
4. [Development Tasks](#development-tasks)
5. [Implementation Sequence](#implementation-sequence)
6. [Testing Strategy](#testing-strategy)
7. [Risk Mitigation](#risk-mitigation)
8. [Success Criteria](#success-criteria)

---

## Overview

Phase 1 is the foundation for all subsequent phases. It transforms the monolithic capture system into a modular, source-based architecture without changing any user-visible behavior. This is purely a refactoring phase.

### Key Principles

1. **Zero Breaking Changes:** All existing APIs must work exactly as before
2. **No Performance Regression:** Maintain or improve current performance
3. **Internal Only:** No UI changes, no new features exposed to users
4. **Test Coverage:** Comprehensive tests to ensure equivalence
5. **Clean Abstractions:** Design interfaces that will support future phases

### What Phase 1 Achieves

- ✅ Separates concerns: capture logic, encoding, and muxing
- ✅ Makes MP4SinkWriter source-agnostic (callback-based)
- ✅ Encapsulates screen capture in a reusable source class
- ✅ Encapsulates audio capture in a reusable source class
- ✅ Establishes patterns for Phase 2's multiple sources
- ✅ Maintains global state for compatibility during transition

### What Phase 1 Does NOT Do

- ❌ Add multiple source support (that's Phase 2)
- ❌ Add multi-track recording (that's Phase 3)
- ❌ Change the UI (that's Phase 5)
- ❌ Add new capture sources (Phase 2)
- ❌ Expose source APIs to C# (incremental in Phase 2+)

---

## Current Architecture Deep Dive

### Component Inventory

**ScreenRecorder.cpp/h:**
- Global static state management
- Exports: `TryStartRecording`, `TryStopRecording`, `TryPauseRecording`, `TryResumeRecording`, `TryToggleAudioCapture`
- Owns: `g_session`, `g_framePool`, `g_frameHandler`, `g_sinkWriter`, `g_audioHandler`
- Coordinates initialization sequence for video and audio capture

**MP4SinkWriter.cpp/h:**
- Combines video encoding (H.264) and audio encoding (AAC)
- Tightly coupled to video frames via `FrameArrivedHandler`
- Tightly coupled to audio samples via `AudioCaptureHandler`
- Manages: Media Foundation sink writer, video/audio stream indices
- Synchronization: Recording start time (QPC-based)

**FrameArrivedHandler.cpp/h:**
- Implements Windows.Graphics.Capture event handler
- Background thread with frame queue
- Directly calls `MP4SinkWriter::WriteFrame()`
- Manages first frame timestamp for relative timing

**AudioCaptureHandler.cpp/h:**
- WASAPI loopback audio capture
- Dedicated capture thread (ABOVE_NORMAL priority)
- Directly calls `MP4SinkWriter::WriteAudioSample()`
- Timestamp accumulation to prevent audio speedup
- Enable/disable support (muting without stopping)

**AudioCaptureDevice.cpp/h:**
- Low-level WASAPI device wrapper
- Endpoint enumeration and activation
- Audio client initialization
- Sample capture from render endpoint (loopback)

### Current Data Flow

```
User → C# → P/Invoke → TryStartRecording()
                            ↓
                    [Initialize D3D11]
                            ↓
                    [Create Graphics.Capture session]
                            ↓
                    [Initialize MP4SinkWriter]
                            ↓
                [Initialize AudioCaptureHandler (optional)]
                            ↓
                [Register FrameArrivedHandler callback]
                            ↓
                    [StartCapture()]

During Recording:
    Graphics.Capture → FrameArrived Event → FrameArrivedHandler::Invoke()
                                                ↓
                                        [Queue frame on background thread]
                                                ↓
                                        MP4SinkWriter::WriteFrame()

    WASAPI → AudioCaptureHandler::CaptureThreadProc()
                    ↓
            MP4SinkWriter::WriteAudioSample()

Stop:
    TryStopRecording() → AudioCaptureHandler::Stop()
                      → FrameArrivedHandler::Stop()
                      → MP4SinkWriter::Finalize()
                      → Session cleanup
```

### Current Coupling Points

1. **FrameArrivedHandler → MP4SinkWriter**
   - Direct member: `wil::com_ptr<MP4SinkWriter> m_sinkWriter`
   - Direct call: `m_sinkWriter->WriteFrame(texture, timestamp)`

2. **AudioCaptureHandler → MP4SinkWriter**
   - Direct member: `MP4SinkWriter* m_sinkWriter`
   - Direct call: `m_sinkWriter->WriteAudioSample(data, frames, timestamp)`
   - Shared synchronization: `m_sinkWriter->GetRecordingStartTime()`

3. **ScreenRecorder → All Components**
   - Global static instances (tight coupling)
   - Direct initialization sequence dependencies
   - Shared device context

### Synchronization Mechanism

All components share a common time base:
- **QPC (QueryPerformanceCounter)** provides high-resolution timestamps
- **Recording Start Time:** Set by first frame/sample, stored in MP4SinkWriter
- **Relative Timestamps:** All subsequent frames/samples use (current QPC - start QPC)
- **100ns Units:** Media Foundation requires timestamps in 100-nanosecond units

---

## Target Architecture

### New Component Hierarchy

```
IMediaSource (base interface)
    ├── IVideoSource (abstract video capture)
    │       └── ScreenCaptureSource (implementation)
    │
    └── IAudioSource (abstract audio capture)
            └── DesktopAudioSource (implementation)

SourceCoordinator (orchestrates multiple sources)
    ├── Manages source lifecycle
    ├── Provides callback registration
    └── Coordinates start/stop

MP4SinkWriter (refactored - callback-based)
    ├── Video callback: void OnVideoFrame(ID3D11Texture2D*, LONGLONG)
    └── Audio callback: void OnAudioSample(const BYTE*, UINT32, LONGLONG)
```

### Interface Design

#### C++ Layer

```cpp
// IMediaSource.h
#pragma once

enum class MediaSourceType
{
    Video,
    Audio
};

class IMediaSource
{
public:
    virtual ~IMediaSource() = default;
    
    // Type identification
    virtual MediaSourceType GetSourceType() const = 0;
    
    // Lifecycle
    virtual bool Initialize() = 0;
    virtual bool Start() = 0;
    virtual void Stop() = 0;
    virtual bool IsRunning() const = 0;
    
    // Reference counting (COM-like pattern)
    virtual ULONG AddRef() = 0;
    virtual ULONG Release() = 0;
};

// IVideoSource.h
#pragma once
#include "IMediaSource.h"

// Video frame callback signature
using VideoFrameCallback = std::function<void(ID3D11Texture2D* texture, LONGLONG timestamp)>;

class IVideoSource : public IMediaSource
{
public:
    // Source properties
    virtual void GetResolution(UINT32& width, UINT32& height) const = 0;
    
    // Callback registration
    virtual void SetFrameCallback(VideoFrameCallback callback) = 0;
    
    // Type override
    MediaSourceType GetSourceType() const override { return MediaSourceType::Video; }
};

// IAudioSource.h
#pragma once
#include "IMediaSource.h"

// Audio sample callback signature
using AudioSampleCallback = std::function<void(const BYTE* data, UINT32 numFrames, LONGLONG timestamp)>;

class IAudioSource : public IMediaSource
{
public:
    // Audio properties
    virtual WAVEFORMATEX* GetFormat() const = 0;
    
    // Callback registration
    virtual void SetAudioCallback(AudioSampleCallback callback) = 0;
    
    // Runtime control
    virtual void SetEnabled(bool enabled) = 0;
    virtual bool IsEnabled() const = 0;
    
    // Type override
    MediaSourceType GetSourceType() const override { return MediaSourceType::Audio; }
};
```

### New Data Flow

```
User → C# → P/Invoke → TryStartRecording()
                            ↓
                [Create ScreenCaptureSource]
                            ↓
                [Create DesktopAudioSource (optional)]
                            ↓
                [Initialize MP4SinkWriter]
                            ↓
        [Register callbacks: source → MP4SinkWriter]
                            ↓
                [Start all sources]

During Recording:
    ScreenCaptureSource → VideoFrameCallback → MP4SinkWriter::WriteFrame()
    DesktopAudioSource  → AudioSampleCallback → MP4SinkWriter::WriteAudioSample()

Stop:
    TryStopRecording() → Stop all sources
                      → MP4SinkWriter::Finalize()
                      → Cleanup
```

---

## Development Tasks

### Task 1: Create Base Interfaces

**File:** `src/CaptureInterop/IMediaSource.h`

**Content:**
```cpp
#pragma once

enum class MediaSourceType
{
    Video,
    Audio
};

class IMediaSource
{
public:
    virtual ~IMediaSource() = default;
    
    virtual MediaSourceType GetSourceType() const = 0;
    virtual bool Initialize() = 0;
    virtual bool Start() = 0;
    virtual void Stop() = 0;
    virtual bool IsRunning() const = 0;
    
    // COM-like reference counting for lifetime management
    virtual ULONG AddRef() = 0;
    virtual ULONG Release() = 0;
};
```

**File:** `src/CaptureInterop/IVideoSource.h`

**Content:** (See interface design above)

**File:** `src/CaptureInterop/IAudioSource.h`

**Content:** (See interface design above)

**Acceptance Criteria:**
- ✅ Files compile without errors
- ✅ Interfaces are pure virtual (no implementation)
- ✅ Headers have proper include guards
- ✅ Added to `CaptureInterop.vcxproj.filters`

---

### Task 2: Implement ScreenCaptureSource

**Goal:** Extract all video capture logic from `ScreenRecorder.cpp` into a reusable source class.

**File:** `src/CaptureInterop/ScreenCaptureSource.h`

**Class Structure:**
```cpp
#pragma once
#include "IVideoSource.h"
#include "FrameArrivedHandler.h"

class ScreenCaptureSource : public IVideoSource
{
public:
    ScreenCaptureSource();
    ~ScreenCaptureSource();

    // Configuration (must be called before Initialize)
    void SetMonitor(HMONITOR hMonitor);
    void SetDevice(ID3D11Device* device);

    // IVideoSource implementation
    void GetResolution(UINT32& width, UINT32& height) const override;
    void SetFrameCallback(VideoFrameCallback callback) override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    // Reference counting
    volatile long m_ref = 1;

    // Configuration
    HMONITOR m_hMonitor = nullptr;
    ID3D11Device* m_device = nullptr;

    // Capture infrastructure
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem> m_captureItem;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureSession> m_session;
    wil::com_ptr<ABI::Windows::Graphics::Capture::IDirect3D11CaptureFramePool> m_framePool;
    EventRegistrationToken m_frameArrivedToken;
    
    // Frame handler (now uses callback instead of direct MP4SinkWriter reference)
    FrameArrivedHandler* m_frameHandler = nullptr;
    VideoFrameCallback m_frameCallback;
    
    // Properties
    UINT32 m_width = 0;
    UINT32 m_height = 0;
    bool m_isRunning = false;

    // Helper methods
    bool InitializeGraphicsCapture(HRESULT* outHr);
    void Cleanup();
};
```

**Implementation Details:**

1. **Constructor/Destructor:**
   - Initialize reference count to 1
   - Destructor calls `Cleanup()` to release resources

2. **SetMonitor/SetDevice:**
   - Store configuration for later initialization
   - Must be called before `Initialize()`

3. **Initialize():**
   - Get `IGraphicsCaptureItemInterop` from helper
   - Create capture item for monitor
   - Create D3D11 capture frame pool
   - Create capture session
   - Query capture size and store in `m_width`/`m_height`
   - **Does NOT start capture** (that's `Start()`)

4. **Start():**
   - Verify initialized state
   - Create `FrameArrivedHandler` with callback bridge
   - Register frame arrived event
   - Call `m_session->StartCapture()`
   - Set `m_isRunning = true`

5. **Stop():**
   - Unregister frame arrived event
   - Stop and release frame handler
   - Reset session
   - Set `m_isRunning = false`

6. **SetFrameCallback():**
   - Store callback for frame handler to use
   - Bridge between `FrameArrivedHandler` and `MP4SinkWriter`

**Modified:** `src/CaptureInterop/FrameArrivedHandler.h`

**Changes:**
- Replace `wil::com_ptr<MP4SinkWriter> m_sinkWriter` with `VideoFrameCallback m_callback`
- Constructor takes callback instead of sink writer
- `ProcessingThreadProc()` calls callback instead of `m_sinkWriter->WriteFrame()`

**File:** `src/CaptureInterop/ScreenCaptureSource.cpp`

**Implementation Notes:**
- Move logic from `ScreenRecorder::TryStartRecording()` lines 24-60, 95-97
- Reuse existing helper functions from `GraphicsCaptureHelpers`
- Error handling: Return false on failure, log errors
- Resource cleanup: Use RAII patterns, release in reverse order

**Acceptance Criteria:**
- ✅ Compiles without errors
- ✅ No direct dependency on `MP4SinkWriter`
- ✅ Uses callbacks for frame delivery
- ✅ Proper resource cleanup in destructor
- ✅ Thread-safe reference counting

---

### Task 3: Implement DesktopAudioSource

**Goal:** Extract audio capture logic into a reusable source class.

**File:** `src/CaptureInterop/DesktopAudioSource.h`

**Class Structure:**
```cpp
#pragma once
#include "IAudioSource.h"
#include "AudioCaptureDevice.h"
#include <thread>
#include <atomic>

class DesktopAudioSource : public IAudioSource
{
public:
    DesktopAudioSource();
    ~DesktopAudioSource();

    // IAudioSource implementation
    WAVEFORMATEX* GetFormat() const override;
    void SetAudioCallback(AudioSampleCallback callback) override;
    void SetEnabled(bool enabled) override;
    bool IsEnabled() const override;

    // IMediaSource implementation
    bool Initialize() override;
    bool Start() override;
    void Stop() override;
    bool IsRunning() const override;
    ULONG AddRef() override;
    ULONG Release() override;

private:
    // Reference counting
    volatile long m_ref = 1;

    // Audio capture
    AudioCaptureDevice m_device;
    AudioSampleCallback m_callback;
    
    // Capture thread
    std::thread m_captureThread;
    std::atomic<bool> m_isRunning{false};
    std::atomic<bool> m_isEnabled{true};
    
    // Synchronization (for integration with MP4SinkWriter)
    LONGLONG m_startQpc = 0;
    LARGE_INTEGER m_qpcFrequency{};
    LONGLONG m_nextAudioTimestamp = 0;
    
    // Silent buffer management
    std::atomic<bool> m_wasDisabled{false};
    std::atomic<int> m_samplesToSkip{0};
    std::vector<BYTE> m_silentBuffer;

    // Thread procedure
    void CaptureThreadProc();
    
    // Cleanup
    void Cleanup();
};
```

**Implementation Details:**

1. **Initialize():**
   - Initialize `AudioCaptureDevice` with loopback=true
   - Get and cache audio format
   - Query QPC frequency for timestamp calculation
   - Allocate silent buffer based on format

2. **Start():**
   - Record start time (QPC)
   - Start capture thread (ABOVE_NORMAL priority)
   - Thread runs `CaptureThreadProc()`

3. **Stop():**
   - Signal thread to stop (`m_isRunning = false`)
   - Join capture thread
   - Stop audio device

4. **CaptureThreadProc():**
   - **Move logic from `AudioCaptureHandler::CaptureThreadProc()`**
   - Poll WASAPI for audio packets
   - Calculate timestamps using accumulated time
   - Handle enable/disable (write silence when disabled)
   - Call `m_callback` instead of directly writing to sink writer

5. **SetEnabled():**
   - Controls whether real audio or silence is sent
   - Used for runtime muting

**Modified:** Extract common logic from `AudioCaptureHandler.cpp`

**File:** `src/CaptureInterop/DesktopAudioSource.cpp`

**Implementation Notes:**
- Reuse `AudioCaptureDevice` class (no changes needed)
- Keep timestamp accumulation logic (prevents audio speedup)
- Maintain enable/disable behavior for muting
- Handle resync after re-enable (skip samples for alignment)

**Acceptance Criteria:**
- ✅ Compiles without errors
- ✅ No direct dependency on `MP4SinkWriter`
- ✅ Uses callbacks for sample delivery
- ✅ Maintains timing precision (accumulated timestamps)
- ✅ Supports runtime enable/disable

---

### Task 4: Refactor MP4SinkWriter for Callback-Based Input

**Goal:** Decouple MP4SinkWriter from specific source implementations.

**File:** `src/CaptureInterop/MP4SinkWriter.h`

**Changes:**
- No structural changes to public API
- Internal: Remove any assumptions about source types
- Keep: All encoding and muxing logic
- Keep: Recording start time management (still needed for sync)

**File:** `src/CaptureInterop/MP4SinkWriter.cpp`

**Changes:**
- **None required** - MP4SinkWriter already uses generic `WriteFrame()` and `WriteAudioSample()` methods
- Already source-agnostic in its design
- Only needs to receive data via callbacks (done by caller, not MP4SinkWriter)

**Verification:**
- ✅ Review that MP4SinkWriter doesn't create or manage sources
- ✅ Verify it only encodes and muxes provided data
- ✅ Confirm callback approach works (sources call WriteFrame/WriteAudioSample)

---

### Task 5: Update ScreenRecorder to Use Source Abstraction

**Goal:** Refactor the global coordinator to use new source classes while maintaining existing API.

**File:** `src/CaptureInterop/ScreenRecorder.cpp`

**Before (Current):**
```cpp
static MP4SinkWriter g_sinkWriter;
static AudioCaptureHandler g_audioHandler;
static FrameArrivedHandler* g_frameHandler = nullptr;
// ... other globals
```

**After (Phase 1):**
```cpp
static MP4SinkWriter g_sinkWriter;
static ScreenCaptureSource* g_videoSource = nullptr;
static DesktopAudioSource* g_audioSource = nullptr;
```

**Modified Function: `TryStartRecording()`**

**New Implementation:**
```cpp
__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio)
{
    HRESULT hr = S_OK;
    
    // Initialize D3D11 device (reuse existing helper)
    D3DDeviceAndContext d3d = InitializeD3D(&hr);
    if (FAILED(hr)) return false;
    
    // Create and configure video source
    g_videoSource = new ScreenCaptureSource();
    g_videoSource->SetMonitor(hMonitor);
    g_videoSource->SetDevice(d3d.device.get());
    
    if (!g_videoSource->Initialize())
    {
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    // Get video resolution for sink writer
    UINT32 width, height;
    g_videoSource->GetResolution(width, height);
    
    // Initialize MP4 sink writer
    if (!g_sinkWriter.Initialize(outputPath, d3d.device.get(), width, height, &hr))
    {
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    // Set up video callback
    g_videoSource->SetFrameCallback([](ID3D11Texture2D* texture, LONGLONG timestamp) {
        g_sinkWriter.WriteFrame(texture, timestamp);
    });
    
    // Create and configure audio source if requested
    bool audioEnabled = false;
    if (captureAudio)
    {
        g_audioSource = new DesktopAudioSource();
        
        if (g_audioSource->Initialize())
        {
            // Initialize audio stream in sink writer
            WAVEFORMATEX* audioFormat = g_audioSource->GetFormat();
            if (audioFormat && g_sinkWriter.InitializeAudioStream(audioFormat, &hr))
            {
                // Set up audio callback
                g_audioSource->SetAudioCallback([](const BYTE* data, UINT32 numFrames, LONGLONG timestamp) {
                    g_sinkWriter.WriteAudioSample(data, numFrames, timestamp);
                });
                
                // Start audio capture
                if (g_audioSource->Start(&hr))
                {
                    audioEnabled = true;
                }
            }
        }
        
        // If audio initialization failed, clean up but continue with video
        if (!audioEnabled && g_audioSource)
        {
            g_audioSource->Release();
            g_audioSource = nullptr;
        }
    }
    
    // Start video capture
    if (!g_videoSource->Start())
    {
        if (g_audioSource)
        {
            g_audioSource->Stop();
            g_audioSource->Release();
            g_audioSource = nullptr;
        }
        g_videoSource->Release();
        g_videoSource = nullptr;
        return false;
    }
    
    return true;
}
```

**Modified Function: `TryStopRecording()`**

**New Implementation:**
```cpp
__declspec(dllexport) void TryStopRecording()
{
    // Stop audio source first
    if (g_audioSource)
    {
        g_audioSource->Stop();
        g_audioSource->Release();
        g_audioSource = nullptr;
    }
    
    // Stop video source
    if (g_videoSource)
    {
        g_videoSource->Stop();
        g_videoSource->Release();
        g_videoSource = nullptr;
    }
    
    // Finalize MP4 file
    g_sinkWriter.Finalize();
}
```

**Modified Function: `TryToggleAudioCapture()`**

**New Implementation:**
```cpp
__declspec(dllexport) void TryToggleAudioCapture(bool enabled)
{
    if (g_audioSource)
    {
        g_audioSource->SetEnabled(enabled);
    }
}
```

**Functions: `TryPauseRecording()` and `TryResumeRecording()`**
- Leave unimplemented (already empty)
- Will be implemented in future phases

**Acceptance Criteria:**
- ✅ All existing P/Invoke exports maintain same signature
- ✅ Behavior is identical to current implementation
- ✅ Global state maintained during transition
- ✅ Error handling preserved
- ✅ Resource cleanup order correct

---

### Task 6: Update FrameArrivedHandler to Use Callbacks

**Goal:** Modify FrameArrivedHandler to use callbacks instead of direct MP4SinkWriter reference.

**File:** `src/CaptureInterop/FrameArrivedHandler.h`

**Changes:**
```cpp
// Before:
explicit FrameArrivedHandler(wil::com_ptr<MP4SinkWriter> sinkWriter) noexcept;
wil::com_ptr<MP4SinkWriter> m_sinkWriter;

// After:
explicit FrameArrivedHandler(VideoFrameCallback callback) noexcept;
VideoFrameCallback m_callback;
```

**File:** `src/CaptureInterop/FrameArrivedHandler.cpp`

**Changes in `ProcessingThreadProc()`:**
```cpp
// Before:
HRESULT hr = m_sinkWriter->WriteFrame(frame.texture.get(), frame.relativeTimestamp);

// After:
if (m_callback)
{
    m_callback(frame.texture.get(), frame.relativeTimestamp);
}
```

**Acceptance Criteria:**
- ✅ Compiles without errors
- ✅ Callback is properly invoked for each frame
- ✅ Thread safety maintained
- ✅ No change in frame processing behavior

---

### Task 7: Update Build Configuration

**File:** `src/CaptureInterop/CaptureInterop.vcxproj`

**Add New Files:**
- `<ClInclude Include="IMediaSource.h" />`
- `<ClInclude Include="IVideoSource.h" />`
- `<ClInclude Include="IAudioSource.h" />`
- `<ClInclude Include="ScreenCaptureSource.h" />`
- `<ClCompile Include="ScreenCaptureSource.cpp" />`
- `<ClInclude Include="DesktopAudioSource.h" />`
- `<ClCompile Include="DesktopAudioSource.cpp" />`

**File:** `src/CaptureInterop/CaptureInterop.vcxproj.filters`

**Add Filters:**
```xml
<Filter Include="Source Abstraction">
  <UniqueIdentifier>{...}</UniqueIdentifier>
</Filter>
```

**Add Items to Filter:**
- All new interface and implementation files under "Source Abstraction" filter

**Acceptance Criteria:**
- ✅ Project builds successfully
- ✅ New files visible in Visual Studio solution explorer
- ✅ Proper organization in filters

---

### Task 8: Add Internal Documentation

**File:** `src/CaptureInterop/README.md` (new)

**Content:**
```markdown
# CaptureInterop Architecture

## Overview

CaptureInterop is the native C++ component that handles screen and audio capture for CaptureTool.

## Source Abstraction (Phase 1)

As of Phase 1, capture logic is organized using a source-based architecture:

### Interfaces

- **IMediaSource:** Base interface for all capture sources
- **IVideoSource:** Interface for video capture sources
- **IAudioSource:** Interface for audio capture sources

### Implementations

- **ScreenCaptureSource:** Windows.Graphics.Capture-based screen recording
- **DesktopAudioSource:** WASAPI loopback audio capture

### Coordinator

- **ScreenRecorder:** Global coordinator that manages source lifecycle and maintains backward compatibility

### Encoding & Muxing

- **MP4SinkWriter:** Media Foundation-based H.264/AAC encoder and MP4 muxer
- **FrameArrivedHandler:** Async video frame processing with callback bridge

## Data Flow

```
User → C# → P/Invoke → ScreenRecorder (coordinator)
                            ↓
                [Creates ScreenCaptureSource]
                [Creates DesktopAudioSource]
                            ↓
                [Registers callbacks → MP4SinkWriter]
                            ↓
                [Starts all sources]

During Recording:
    ScreenCaptureSource → callback → MP4SinkWriter::WriteFrame()
    DesktopAudioSource → callback → MP4SinkWriter::WriteAudioSample()
```

## Future Phases

- **Phase 2:** Add MicrophoneAudioSource, ApplicationAudioSource, SourceManager
- **Phase 3:** Add AudioMixer for multi-track routing
- **Phase 4:** Separate encoding pipeline with encoder interfaces
- **Phase 5:** Enhanced UI for source management
```

**Acceptance Criteria:**
- ✅ README exists and explains architecture
- ✅ Developers can understand code organization
- ✅ References to future phases

---

## Implementation Sequence

### Week 1: Foundation

**Days 1-2: Interface Definition**
- [ ] Create `IMediaSource.h`
- [ ] Create `IVideoSource.h`
- [ ] Create `IAudioSource.h`
- [ ] Update build configuration
- [ ] Verify compilation

**Days 3-5: ScreenCaptureSource**
- [ ] Create `ScreenCaptureSource.h`
- [ ] Implement `ScreenCaptureSource.cpp`
- [ ] Modify `FrameArrivedHandler` for callbacks
- [ ] Unit test source lifecycle
- [ ] Verify video capture works in isolation

### Week 2: Audio and Integration

**Days 1-3: DesktopAudioSource**
- [ ] Create `DesktopAudioSource.h`
- [ ] Implement `DesktopAudioSource.cpp`
- [ ] Extract timing logic from AudioCaptureHandler
- [ ] Unit test audio capture
- [ ] Verify audio capture works in isolation

**Days 4-5: Integration**
- [ ] Refactor `ScreenRecorder.cpp` to use sources
- [ ] Update all exported functions
- [ ] Integration testing
- [ ] Performance testing
- [ ] Regression testing

### Week 3: Testing and Documentation

**Days 1-2: Comprehensive Testing**
- [ ] Test all existing scenarios work
- [ ] Test error handling
- [ ] Test resource cleanup
- [ ] Long-duration stability test
- [ ] Performance comparison (before/after)

**Days 3-4: Documentation and Polish**
- [ ] Add code comments
- [ ] Create internal README
- [ ] Update architecture documentation
- [ ] Code review
- [ ] Address feedback

**Day 5: Final Verification**
- [ ] Full regression test suite
- [ ] Performance benchmarks
- [ ] Memory leak detection
- [ ] Prepare for merge

---

## Testing Strategy

### Unit Tests

**ScreenCaptureSource Tests:**
```cpp
TEST(ScreenCaptureSource, InitializeWithValidMonitor)
TEST(ScreenCaptureSource, StartStopLifecycle)
TEST(ScreenCaptureSource, CallbackInvoked)
TEST(ScreenCaptureSource, GetResolution)
TEST(ScreenCaptureSource, ReferenceCountingWorks)
```

**DesktopAudioSource Tests:**
```cpp
TEST(DesktopAudioSource, InitializeSuccess)
TEST(DesktopAudioSource, StartStopLifecycle)
TEST(DesktopAudioSource, CallbackInvoked)
TEST(DesktopAudioSource, EnableDisableControl)
TEST(DesktopAudioSource, TimestampAccumulation)
```

### Integration Tests

**Full Recording Flow:**
- [ ] Screen capture only (no audio)
- [ ] Screen capture with desktop audio
- [ ] Toggle audio during recording
- [ ] Stop and restart recording
- [ ] Multiple sequential recordings

**Error Scenarios:**
- [ ] Invalid monitor handle
- [ ] No audio device available
- [ ] Disk full during recording
- [ ] Source stop during recording
- [ ] Multiple start calls without stop

### Performance Tests

**Metrics to Measure:**
- [ ] CPU usage (should be ≤ current)
- [ ] Memory usage (should be ≤ current)
- [ ] Frame drop rate (should be 0)
- [ ] Audio/video sync accuracy (<10ms)
- [ ] Time to start recording (<500ms)

**Scenarios:**
- [ ] 5-minute recording (baseline)
- [ ] 30-minute recording (stability)
- [ ] 1080p @ 60fps (high load)
- [ ] 4K @ 30fps (high resolution)

### Regression Tests

**Use existing test suite:**
- [ ] All current capture tests pass
- [ ] No new memory leaks (valgrind/ASAN)
- [ ] No performance degradation
- [ ] Same output file quality
- [ ] Same synchronization accuracy

---

## Risk Mitigation

### Risk 1: Breaking Existing Functionality

**Likelihood:** Medium  
**Impact:** High

**Mitigation:**
- Maintain all existing function signatures
- Use global state during transition (allows rollback)
- Comprehensive regression testing before merge
- Gradual refactoring (one component at a time)
- Keep old implementation as reference during development

**Rollback Plan:**
- Git revert to pre-Phase-1 commit
- All changes in feature branch (no main branch impact)

### Risk 2: Performance Regression

**Likelihood:** Low  
**Impact:** High

**Mitigation:**
- Profile before and after refactoring
- Minimize callback overhead (inline where possible)
- Maintain direct paths for critical sections
- Use benchmarks to detect regressions early
- Lambda captures by value for callbacks (avoid heap allocations)

**Indicators:**
- CPU usage increase >5%
- Frame drops detected
- Audio glitches during capture

### Risk 3: Synchronization Issues

**Likelihood:** Medium  
**Impact:** High

**Mitigation:**
- Preserve exact timing logic from current implementation
- Test A/V sync extensively
- Use same QPC-based timestamps
- Keep accumulated timestamp approach for audio
- Verify sync in various scenarios (short/long recordings)

**Testing:**
- Record 10-minute video with audio
- Analyze A/V sync at start, middle, end
- Compare against current implementation
- Use tools like FFprobe to measure sync

### Risk 4: Memory Leaks or Resource Issues

**Likelihood:** Low  
**Impact:** Medium

**Mitigation:**
- Use smart pointers (wil::com_ptr) everywhere
- Implement proper reference counting
- Test with memory leak detectors
- Long-duration testing (2+ hours)
- Verify cleanup in destructors

**Detection:**
- Windows Task Manager monitoring
- Visual Studio memory profiler
- Long-duration stress tests

### Risk 5: Threading Issues

**Likelihood:** Medium  
**Impact:** Medium

**Mitigation:**
- Maintain thread-per-source model
- Use atomic operations for shared state
- Careful mutex usage (avoid deadlocks)
- Test stop/start/stop sequences
- Thread sanitizer in debug builds

**Testing:**
- Rapid start/stop cycles
- Stop during active capture
- Multiple sources starting/stopping

---

## Success Criteria

### Functional Requirements

- ✅ All existing capture scenarios work identically
- ✅ `TryStartRecording()` starts recording successfully
- ✅ `TryStopRecording()` stops and finalizes MP4 correctly
- ✅ `TryToggleAudioCapture()` mutes/unmutes audio
- ✅ Screen-only recording works
- ✅ Screen + desktop audio recording works
- ✅ Output MP4 files are valid and playable
- ✅ Audio/video synchronization maintained

### Performance Requirements

- ✅ CPU usage ≤ current baseline
- ✅ Memory usage ≤ current baseline
- ✅ Zero frame drops (same as current)
- ✅ A/V sync accuracy <10ms
- ✅ Recording start time <500ms

### Code Quality Requirements

- ✅ All new code compiles without warnings
- ✅ Follows existing code style
- ✅ Proper error handling (return codes, exceptions)
- ✅ No memory leaks detected
- ✅ Thread-safe reference counting
- ✅ Clean abstractions (single responsibility)
- ✅ Internal documentation exists

### Testing Requirements

- ✅ All existing tests pass
- ✅ New unit tests for source classes
- ✅ Integration tests for refactored coordinator
- ✅ Performance benchmarks show no regression
- ✅ Long-duration stability test (2+ hours)

### Documentation Requirements

- ✅ Internal README explains new architecture
- ✅ Code comments on non-obvious logic
- ✅ Architecture diagram updated
- ✅ Phase 1 completion report

---

## Post-Phase 1 State

### What We Have

After Phase 1 is complete, the codebase will have:

1. **Clean Abstractions:**
   - IMediaSource, IVideoSource, IAudioSource interfaces
   - ScreenCaptureSource and DesktopAudioSource implementations
   - Callback-based communication between sources and muxer

2. **Maintained Compatibility:**
   - All existing P/Invoke APIs work unchanged
   - Same user experience
   - Same performance characteristics
   - Same output quality

3. **Foundation for Phase 2:**
   - Pattern established for adding new sources
   - Callback mechanism proven
   - Lifecycle management tested
   - Ready to add MicrophoneAudioSource, ApplicationAudioSource

### What's Next (Phase 2)

- Add MicrophoneAudioSource (capture endpoint, not loopback)
- Add ApplicationAudioSource (per-process audio via Audio Session API)
- Create SourceManager for coordinating multiple sources
- Expose source enumeration and selection to C# layer
- Update UI to show available sources (Phase 5)

---

## Appendix A: Code Review Checklist

### For Each New File

- [ ] Proper include guards or `#pragma once`
- [ ] Includes ordered: PCH, system, project, local
- [ ] No unused includes
- [ ] Namespace usage appropriate
- [ ] Comments on non-obvious logic

### For Interfaces

- [ ] Pure virtual methods (= 0)
- [ ] Virtual destructor
- [ ] No data members
- [ ] Minimal, focused contract

### For Implementations

- [ ] Implements all interface methods
- [ ] Reference counting correct (AddRef/Release)
- [ ] Initialize() idempotent
- [ ] Start() checks initialized state
- [ ] Stop() safe to call multiple times
- [ ] Proper cleanup in destructor
- [ ] Thread-safe where needed

### For Modified Files

- [ ] Functionality preserved
- [ ] No breaking changes
- [ ] Error handling maintained
- [ ] Comments updated
- [ ] Dead code removed

### For Callbacks

- [ ] Signature matches usage
- [ ] Captures minimized
- [ ] Thread-safe invocation
- [ ] Null checks before calling
- [ ] Error handling in callback

---

## Appendix B: Performance Profiling Plan

### Baseline Measurement (Before Phase 1)

**Test Scenario:** 5-minute 1080p recording with desktop audio

**Metrics:**
- Average CPU usage: __%
- Peak CPU usage: __%
- Average memory usage: __ MB
- Peak memory usage: __ MB
- Frames captured: ____
- Frames dropped: __
- Average A/V sync offset: __ ms
- Recording start time: __ ms

### Target Measurement (After Phase 1)

**Test Scenario:** Same as baseline

**Metrics:**
- Average CPU usage: ≤ baseline + 2%
- Peak CPU usage: ≤ baseline + 5%
- Average memory usage: ≤ baseline + 5 MB
- Peak memory usage: ≤ baseline + 10 MB
- Frames captured: same
- Frames dropped: 0
- Average A/V sync offset: ≤ 10 ms
- Recording start time: ≤ 500 ms

### Profiling Tools

- Visual Studio Performance Profiler (CPU usage)
- Windows Performance Analyzer (detailed tracing)
- Task Manager (quick checks)
- FFprobe (A/V sync verification)

### Profiling Points

1. **During Initialize():**
   - D3D11 device creation
   - Graphics.Capture session setup
   - WASAPI audio device enumeration

2. **During Recording:**
   - Frame capture callback overhead
   - Audio sample callback overhead
   - Encoding time (H.264, AAC)
   - Muxing overhead

3. **During Stop():**
   - Resource cleanup time
   - MP4 finalization time

---

## Appendix C: Git Workflow

### Branch Strategy

```
main
  └── copilot/plan-obs-capture-support (planning - current)
        └── phase-1/source-abstraction (implementation - new)
```

### Commit Strategy

**Small, Focused Commits:**
- One logical change per commit
- Descriptive commit messages
- Reference task numbers

**Example Commits:**
```
[Phase 1] Add IMediaSource base interface
[Phase 1] Implement ScreenCaptureSource
[Phase 1] Refactor FrameArrivedHandler to use callbacks
[Phase 1] Update ScreenRecorder to use source abstraction
[Phase 1] Add unit tests for ScreenCaptureSource
```

### Code Review Process

1. Self-review changes before commit
2. Run all tests locally
3. Performance profiling comparison
4. Create PR from phase-1 branch
5. Address feedback
6. Squash if needed (keep history clean)

---

## Appendix D: Troubleshooting Guide

### Issue: Compilation Errors After Adding New Files

**Symptoms:** Build fails with "cannot find file" errors

**Solutions:**
- Verify files added to .vcxproj
- Check include paths
- Ensure `pch.h` included first
- Clean and rebuild solution

### Issue: Linking Errors for New Classes

**Symptoms:** "unresolved external symbol" errors

**Solutions:**
- Check .cpp files added to project
- Verify __declspec(dllexport) on exported functions
- Ensure no missing implementations

### Issue: Recording Fails to Start

**Symptoms:** TryStartRecording() returns false

**Debug Steps:**
- Check HRESULT error codes
- Verify monitor handle is valid
- Ensure D3D11 device initialized
- Check audio device availability
- Review error logs

### Issue: A/V Sync Drift

**Symptoms:** Audio progressively out of sync with video

**Debug Steps:**
- Verify QPC frequency query
- Check timestamp accumulation logic
- Ensure recording start time shared
- Profile for frame drops
- Test with shorter recordings

### Issue: Memory Leaks

**Symptoms:** Memory usage grows over time

**Debug Steps:**
- Check AddRef/Release balance
- Verify smart pointer usage
- Look for circular references
- Run with memory leak detector
- Review cleanup in destructors

---

**Document Version:** 1.0  
**Last Updated:** 2025-12-18  
**Author:** GitHub Copilot (Phase 1 Planning Session)
