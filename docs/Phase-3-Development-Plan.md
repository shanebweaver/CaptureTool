# Phase 3: Audio Mixer System - Development Plan

**Status:** 📋 Planning Complete - Ready for Implementation  
**Estimated Duration:** 4-5 weeks  
**Complexity:** High  
**Dependencies:** Phase 1 ✅ Complete, Phase 2 ✅ Complete

---

## Executive Summary

Phase 3 transforms CaptureTool from single-source audio capture to a full-featured multi-source audio mixing system with multi-track recording capabilities. This phase implements the core audio mixer, extends MP4SinkWriter to support up to 6 audio tracks, and provides per-source volume control and routing configuration.

**Key Deliverables:**
- Multi-source audio mixer with real-time mixing
- Multi-track MP4SinkWriter (up to 6 AAC audio tracks)
- Per-source volume and mute controls
- Sample rate conversion and format normalization
- Audio routing configuration API
- C# layer integration for mixer control

---

## Table of Contents

1. [Current State Analysis](#current-state-analysis)
2. [Phase 3 Goals](#phase-3-goals)
3. [Architecture Overview](#architecture-overview)
4. [Implementation Tasks](#implementation-tasks)
5. [Implementation Timeline](#implementation-timeline)
6. [Testing Strategy](#testing-strategy)
7. [Risk Mitigation](#risk-mitigation)
8. [Success Criteria](#success-criteria)
9. [Appendices](#appendices)

---

## Current State Analysis

### What We Have (Post-Phase 2)

**Audio Sources:**
- ✅ DesktopAudioSource - System audio loopback
- ✅ MicrophoneAudioSource - Microphone capture
- ✅ ApplicationAudioSource - Per-app audio framework (Windows 11 22H2+)
- ✅ All sources implement IAudioSource interface
- ✅ Timestamp accumulation for proper sync

**Infrastructure:**
- ✅ SourceManager - Thread-safe source coordination
- ✅ Audio device enumeration via AudioDeviceEnumerator
- ✅ ScreenRecorder with dual-path (legacy + source-based)
- ✅ MP4SinkWriter with single audio track support

**Limitations:**
- ❌ No audio mixing - only one audio source can write at a time
- ❌ Single audio track in MP4 output
- ❌ No per-source volume control
- ❌ No sample rate conversion (all sources must match)
- ❌ No format normalization (all sources must use same format)
- ❌ Microphone captured but not written to file

### What Phase 3 Will Add

**Audio Mixer:**
- Multi-source audio mixing with real-time combination
- Per-source volume control (0.0 - 1.0 range)
- Per-source mute/unmute
- Sample rate conversion (SRC) for mismatched sources
- Format normalization (mono→stereo, different bit depths)
- Low-latency ring buffer architecture

**Multi-Track Recording:**
- MP4SinkWriter extended to support 6 audio tracks
- Per-track routing (assign sources to specific tracks)
- Independent AAC encoding per track
- Track metadata (names, language codes)
- Separate track vs. mixed track modes

**Configuration APIs:**
- Audio routing configuration (source → track mapping)
- Volume configuration per source
- Mixer output routing
- C# layer integration

---

## Phase 3 Goals

### Primary Goals

1. **Implement AudioMixer**
   - Combine multiple audio sources in real-time
   - Per-source volume and mute controls
   - Sample rate conversion (SRC)
   - Format normalization

2. **Extend MP4SinkWriter**
   - Support up to 6 audio tracks
   - Independent AAC encoding per track
   - Track metadata support
   - Backward compatibility with single-track mode

3. **Audio Routing System**
   - Configure source → track assignments
   - Support for both mixed and separate track modes
   - Runtime routing changes
   - C# layer configuration APIs

4. **Integration and Testing**
   - Integrate mixer with ScreenRecorder
   - Test multi-source scenarios (desktop + mic)
   - Verify multi-track MP4 files in professional tools (Premiere, DaVinci)
   - Performance validation (<5% CPU overhead)

### Secondary Goals

- Prepare infrastructure for Phase 4 (separate encoder interfaces)
- Document mixer architecture for future extensions
- Establish patterns for real-time audio processing

---

## Architecture Overview

### High-Level Data Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Audio Sources                              │
├──────────────────┬──────────────────┬──────────────────────────────┤
│ DesktopAudio     │ Microphone       │ ApplicationAudio             │
│ (48kHz stereo)   │ (48kHz stereo)   │ (44.1kHz stereo)             │
└────────┬─────────┴────────┬─────────┴────────┬────────────────────┘
         │                  │                  │
         └──────────────────┼──────────────────┘
                            │
                    ┌───────▼────────┐
                    │  AudioMixer    │
                    │  - Volume      │
                    │  - SRC         │
                    │  - Mixing      │
                    └───────┬────────┘
                            │
         ┌──────────────────┼──────────────────┐
         │                  │                  │
    ┌────▼────┐        ┌────▼────┐       ┌────▼────┐
    │ Track 1 │        │ Track 2 │  ...  │ Track 6 │
    │ Desktop │        │  Mic    │       │ App     │
    └────┬────┘        └────┬────┘       └────┬────┘
         │                  │                  │
         └──────────────────┼──────────────────┘
                            │
                    ┌───────▼────────┐
                    │ MP4SinkWriter  │
                    │ (Multi-track)  │
                    └───────┬────────┘
                            │
                        ┌───▼────┐
                        │  MP4   │
                        │  File  │
                        └────────┘
```

### Component Architecture

**AudioMixer:**
- Accepts audio samples from multiple IAudioSource instances
- Maintains per-source ring buffers for temporal alignment
- Applies per-source volume scaling
- Performs sample rate conversion (SRC) if needed
- Mixes samples for combined tracks or routes to separate tracks
- Outputs to MP4SinkWriter track callbacks

**MP4SinkWriter Extensions:**
- `InitializeAudioStream(WAVEFORMATEX*, int trackIndex, const wchar_t* trackName)`
- `WriteAudioSample(const BYTE*, UINT32, LONGLONG, int trackIndex)`
- Internal management of up to 6 audio stream indices
- Track metadata storage and MP4 atom generation

**AudioRoutingConfig:**
- Configures source → track mapping
- Stores per-source volume and mute state
- Provides runtime reconfiguration APIs

---

## Implementation Tasks

### Task 1: Implement AudioMixer Core (Week 1)

**Goal:** Create the core AudioMixer class that can combine multiple audio sources in real-time.

#### 1.1 AudioMixer Class Structure

**File:** `src/CaptureInterop/AudioMixer.h`

```cpp
#pragma once
#include "IAudioSource.h"
#include <map>
#include <vector>
#include <mutex>
#include <functional>

// Forward declarations
struct IMFSample;
struct IMFMediaBuffer;

/// <summary>
/// Audio mixer that combines multiple audio sources with per-source volume control.
/// Handles sample rate conversion, format normalization, and multi-track routing.
/// </summary>
class AudioMixer
{
public:
    AudioMixer();
    ~AudioMixer();

    /// <summary>
    /// Initialize the mixer with target output format.
    /// </summary>
    /// <param name="sampleRate">Target sample rate (e.g., 48000)</param>
    /// <param name="channels">Target channel count (1=mono, 2=stereo)</param>
    /// <param name="bitsPerSample">Bits per sample (16 or 32)</param>
    bool Initialize(UINT32 sampleRate, UINT16 channels, UINT16 bitsPerSample);

    /// <summary>
    /// Add an audio source to the mixer.
    /// </summary>
    /// <param name="source">Audio source to add</param>
    /// <param name="trackIndex">Track index (0-5, -1 for mixed track)</param>
    /// <param name="volume">Initial volume (0.0 - 1.0)</param>
    void AddSource(IAudioSource* source, int trackIndex, float volume = 1.0f);

    /// <summary>
    /// Remove an audio source from the mixer.
    /// </summary>
    void RemoveSource(IAudioSource* source);

    /// <summary>
    /// Set volume for a specific source.
    /// </summary>
    /// <param name="source">Audio source</param>
    /// <param name="volume">Volume (0.0 = mute, 1.0 = full)</param>
    void SetSourceVolume(IAudioSource* source, float volume);

    /// <summary>
    /// Get volume for a specific source.
    /// </summary>
    float GetSourceVolume(IAudioSource* source) const;

    /// <summary>
    /// Mute or unmute a specific source.
    /// </summary>
    void SetSourceMuted(IAudioSource* source, bool muted);

    /// <summary>
    /// Check if a source is muted.
    /// </summary>
    bool IsSourceMuted(IAudioSource* source) const;

    /// <summary>
    /// Get the track index for a source.
    /// </summary>
    int GetSourceTrack(IAudioSource* source) const;

    /// <summary>
    /// Set the track index for a source (-1 for mixed, 0-5 for separate tracks).
    /// </summary>
    void SetSourceTrack(IAudioSource* source, int trackIndex);

    /// <summary>
    /// Process audio samples from a source.
    /// Called by audio source callbacks.
    /// </summary>
    void ProcessAudioSamples(IAudioSource* source, const BYTE* data, UINT32 numFrames, LONGLONG timestamp);

    /// <summary>
    /// Set callback for mixed audio output (all sources combined).
    /// </summary>
    void SetMixedOutputCallback(std::function<void(const BYTE*, UINT32, LONGLONG)> callback);

    /// <summary>
    /// Set callback for per-track audio output.
    /// </summary>
    void SetTrackOutputCallback(std::function<void(const BYTE*, UINT32, LONGLONG, int trackIndex)> callback);

    ULONG STDMETHODCALLTYPE AddRef();
    ULONG STDMETHODCALLTYPE Release();

private:
    struct SourceInfo
    {
        IAudioSource* source;
        int trackIndex;  // -1 = mixed, 0-5 = separate track
        float volume;
        bool muted;
        WAVEFORMATEX format;
        bool needsResampling;
        std::vector<BYTE> resampleBuffer;
    };

    volatile long m_ref = 1;
    std::map<IAudioSource*, SourceInfo> m_sources;
    mutable std::mutex m_mutex;

    // Target format
    UINT32 m_sampleRate;
    UINT16 m_channels;
    UINT16 m_bitsPerSample;
    UINT32 m_blockAlign;

    // Output callbacks
    std::function<void(const BYTE*, UINT32, LONGLONG)> m_mixedCallback;
    std::function<void(const BYTE*, UINT32, LONGLONG, int)> m_trackCallback;

    // Internal mixing
    std::vector<float> m_mixBuffer;  // Temporary buffer for mixing

    // Helper methods
    void MixSamples(const std::vector<const BYTE*>& sources, const std::vector<UINT32>& frameCounts, 
                    const std::vector<float>& volumes, BYTE* output, UINT32 outputFrames);
    void ConvertToFloat(const BYTE* input, float* output, UINT32 numFrames, const WAVEFORMATEX& format);
    void ConvertFromFloat(const float* input, BYTE* output, UINT32 numFrames);
    void ApplyVolume(float* samples, UINT32 numSamples, float volume);
    bool ResampleIfNeeded(const BYTE* input, UINT32 inputFrames, const WAVEFORMATEX& inputFormat,
                          std::vector<BYTE>& output, UINT32& outputFrames);
};
```

#### 1.2 AudioMixer Implementation

**File:** `src/CaptureInterop/AudioMixer.cpp`

Key implementation details:
- **Format Conversion:** Convert all incoming audio to float32 for mixing
- **Sample Rate Conversion:** Use Media Foundation's resampler (IMFTransform with CLSID_CResamplerMediaObject)
- **Volume Scaling:** Apply volume in float domain before conversion back
- **Mixing Algorithm:** Simple addition with clipping prevention
- **Thread Safety:** Mutex protection for source map and callbacks

**Acceptance Criteria:**
- [ ] AudioMixer can accept multiple audio sources
- [ ] Per-source volume control works (0.0 - 1.0 range)
- [ ] Mute/unmute functions correctly
- [ ] Sample rate conversion handles 44.1kHz, 48kHz, 96kHz sources
- [ ] Format conversion handles 16-bit PCM and 32-bit float
- [ ] Mixed output callback fires with combined audio
- [ ] Track output callback fires per-track
- [ ] No audio artifacts (clicks, pops, distortion)
- [ ] CPU usage <5% for 3 simultaneous sources

---

### Task 2: Extend MP4SinkWriter for Multi-Track (Week 2)

**Goal:** Extend MP4SinkWriter to support up to 6 audio tracks.

#### 2.1 MP4SinkWriter Interface Changes

**File:** `src/CaptureInterop/MP4SinkWriter.h`

Add/modify:
```cpp
/// <summary>
/// Initialize an audio stream for the MP4 file.
/// Can be called multiple times to add up to 6 audio tracks.
/// </summary>
/// <param name="audioFormat">Audio format from WASAPI or mixer</param>
/// <param name="trackIndex">Track index (0-5)</param>
/// <param name="trackName">Optional track name (e.g., "Desktop Audio", "Microphone")</param>
/// <param name="outHr">Optional pointer to receive HRESULT</param>
bool InitializeAudioStream(WAVEFORMATEX* audioFormat, int trackIndex, 
                           const wchar_t* trackName = nullptr, HRESULT* outHr = nullptr);

/// <summary>
/// Write an audio sample to a specific track.
/// </summary>
/// <param name="pData">Audio data buffer</param>
/// <param name="numFrames">Number of audio frames</param>
/// <param name="timestamp">Timestamp in 100ns units</param>
/// <param name="trackIndex">Track index (0-5)</param>
HRESULT WriteAudioSample(const BYTE* pData, UINT32 numFrames, LONGLONG timestamp, int trackIndex);

/// <summary>
/// Get the number of audio tracks initialized.
/// </summary>
int GetAudioTrackCount() const { return m_audioTrackCount; }

/// <summary>
/// Check if a specific track index is initialized.
/// </summary>
bool HasAudioTrack(int trackIndex) const;
```

Private members to add:
```cpp
static const int MAX_AUDIO_TRACKS = 6;
DWORD m_audioStreamIndices[MAX_AUDIO_TRACKS];  // IMF stream indices
bool m_audioTrackInitialized[MAX_AUDIO_TRACKS];
WAVEFORMATEX m_audioFormats[MAX_AUDIO_TRACKS];
std::wstring m_trackNames[MAX_AUDIO_TRACKS];
int m_audioTrackCount = 0;
```

#### 2.2 Implementation Details

**Key Changes:**
1. **Track Initialization Loop:** Allow multiple `InitializeAudioStream()` calls with different track indices
2. **Stream Index Mapping:** Track Media Foundation stream indices per audio track
3. **Sample Writing:** Route `WriteAudioSample()` calls to correct MF stream index based on trackIndex
4. **Track Metadata:** Store track names for future MP4 metadata writing
5. **Backward Compatibility:** Single-track mode (trackIndex=0) works as before

**Media Foundation Considerations:**
- Each audio track requires a separate `AddStream()` call
- Each track gets its own AAC encoder instance
- MP4 container supports multiple audio tracks natively
- Track metadata written to MP4 atoms (trak, mdia, hdlr)

**Acceptance Criteria:**
- [ ] Can initialize up to 6 audio tracks
- [ ] Each track has independent AAC encoding
- [ ] WriteAudioSample() correctly routes to track-specific stream
- [ ] Backward compatibility: single track (trackIndex=0) works as before
- [ ] MP4 files with multiple tracks open in media players
- [ ] Track names visible in professional tools (Premiere, DaVinci)
- [ ] No stream index conflicts or mixing

---

### Task 3: Audio Routing Configuration (Week 3)

**Goal:** Implement configuration system for source→track routing.

#### 3.1 AudioRoutingConfig Class

**File:** `src/CaptureInterop/AudioRoutingConfig.h`

```cpp
#pragma once
#include <map>
#include <string>
#include "IAudioSource.h"

/// <summary>
/// Configuration for audio routing: which sources go to which tracks.
/// Supports both mixed mode (all to one track) and separate track mode.
/// </summary>
class AudioRoutingConfig
{
public:
    AudioRoutingConfig();

    /// <summary>
    /// Set the track index for a source.
    /// </summary>
    /// <param name="sourceId">Unique source identifier</param>
    /// <param name="trackIndex">Track index (-1 for mixed, 0-5 for separate)</param>
    void SetSourceTrack(uint64_t sourceId, int trackIndex);

    /// <summary>
    /// Get the track index for a source.
    /// </summary>
    int GetSourceTrack(uint64_t sourceId) const;

    /// <summary>
    /// Set volume for a source.
    /// </summary>
    void SetSourceVolume(uint64_t sourceId, float volume);

    /// <summary>
    /// Get volume for a source.
    /// </summary>
    float GetSourceVolume(uint64_t sourceId) const;

    /// <summary>
    /// Set mute state for a source.
    /// </summary>
    void SetSourceMuted(uint64_t sourceId, bool muted);

    /// <summary>
    /// Check if source is muted.
    /// </summary>
    bool IsSourceMuted(uint64_t sourceId) const;

    /// <summary>
    /// Set track name.
    /// </summary>
    void SetTrackName(int trackIndex, const wchar_t* name);

    /// <summary>
    /// Get track name.
    /// </summary>
    const wchar_t* GetTrackName(int trackIndex) const;

    /// <summary>
    /// Check if using mixed mode (all sources to one track).
    /// </summary>
    bool IsMixedMode() const;

    /// <summary>
    /// Set mixed mode.
    /// </summary>
    void SetMixedMode(bool mixed);

private:
    struct SourceConfig
    {
        int trackIndex;
        float volume;
        bool muted;
    };

    std::map<uint64_t, SourceConfig> m_sourceConfigs;
    std::map<int, std::wstring> m_trackNames;
    bool m_mixedMode = false;
};
```

#### 3.2 C++ Exports for Configuration

**File:** `src/CaptureInterop/ScreenRecorder.cpp` (add functions)

```cpp
// Export functions for audio routing configuration
extern "C"
{
    __declspec(dllexport) void SetAudioSourceTrack(uint64_t sourceHandle, int trackIndex);
    __declspec(dllexport) int GetAudioSourceTrack(uint64_t sourceHandle);
    __declspec(dllexport) void SetAudioSourceVolume(uint64_t sourceHandle, float volume);
    __declspec(dllexport) float GetAudioSourceVolume(uint64_t sourceHandle);
    __declspec(dllexport) void SetAudioSourceMuted(uint64_t sourceHandle, bool muted);
    __declspec(dllexport) bool GetAudioSourceMuted(uint64_t sourceHandle);
    __declspec(dllexport) void SetAudioTrackName(int trackIndex, const wchar_t* name);
    __declspec(dllexport) void SetAudioMixingMode(bool mixedMode);
}
```

**Acceptance Criteria:**
- [ ] Can configure source→track mapping
- [ ] Can set per-source volume (0.0 - 1.0)
- [ ] Can mute/unmute individual sources
- [ ] Can set track names
- [ ] Mixed mode vs separate track mode configurable
- [ ] Configuration persists across recording sessions
- [ ] C++ exports work correctly

---

### Task 4: Integrate AudioMixer with ScreenRecorder (Week 3-4)

**Goal:** Wire AudioMixer into ScreenRecorder for multi-source recording.

#### 4.1 ScreenRecorder Changes

**File:** `src/CaptureInterop/ScreenRecorder.cpp`

Key modifications:
1. **Add AudioMixer Instance:** Create global `AudioMixer* g_audioMixer`
2. **Initialize Mixer:** In `TryStartRecording()`, initialize mixer with target format (48kHz, stereo, 16-bit)
3. **Add Sources to Mixer:** Add DesktopAudioSource and MicrophoneAudioSource to mixer
4. **Configure Callbacks:** Set mixer output callbacks to write to MP4SinkWriter
5. **Source Callbacks:** Update audio source callbacks to call `mixer->ProcessAudioSamples()`

**Pseudocode:**
```cpp
bool TryStartRecording(/* ... */)
{
    // ... existing video setup ...

    // Initialize multi-track MP4SinkWriter
    g_mp4SinkWriter->Initialize(/* ... */);
    
    if (captureDesktopAudio)
    {
        g_mp4SinkWriter->InitializeAudioStream(&desktopFormat, 0, L"Desktop Audio");
    }
    
    if (captureMicrophone)
    {
        g_mp4SinkWriter->InitializeAudioStream(&micFormat, 1, L"Microphone");
    }

    // Initialize AudioMixer
    g_audioMixer = new AudioMixer();
    g_audioMixer->Initialize(48000, 2, 16);

    // Add sources to mixer
    if (g_desktopAudioSource)
    {
        g_audioMixer->AddSource(g_desktopAudioSource, 0, 1.0f);  // Track 0
        g_desktopAudioSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts, void* ctx) {
            g_audioMixer->ProcessAudioSamples(g_desktopAudioSource, data, frames, ts);
        }, nullptr);
    }

    if (g_microphoneSource)
    {
        g_audioMixer->AddSource(g_microphoneSource, 1, 1.0f);  // Track 1
        g_microphoneSource->SetAudioCallback([](const BYTE* data, UINT32 frames, LONGLONG ts, void* ctx) {
            g_audioMixer->ProcessAudioSamples(g_microphoneSource, data, frames, ts);
        }, nullptr);
    }

    // Set mixer output callback
    g_audioMixer->SetTrackOutputCallback([](const BYTE* data, UINT32 frames, LONGLONG ts, int track) {
        g_mp4SinkWriter->WriteAudioSample(data, frames, ts, track);
    });

    // ... start sources ...
}
```

#### 4.2 Cleanup and Teardown

Update `TryStopRecording()`:
- Release AudioMixer
- Ensure all sources stop cleanly
- Finalize MP4SinkWriter (handles multi-track finalization)

**Acceptance Criteria:**
- [ ] Desktop audio writes to Track 0
- [ ] Microphone audio writes to Track 1
- [ ] Both tracks appear in output MP4
- [ ] A/V sync maintained across both tracks
- [ ] No audio dropouts or artifacts
- [ ] Clean start/stop without crashes or leaks

---

### Task 5: C# Layer Integration (Week 4)

**Goal:** Expose audio mixer configuration to C# layer.

#### 5.1 C# Interfaces

**File:** `src/CaptureTool.Domains.Capture.Interfaces/IAudioMixerConfiguration.cs`

```csharp
public interface IAudioMixerConfiguration
{
    /// <summary>
    /// Set the track index for an audio source.
    /// </summary>
    void SetSourceTrack(ulong sourceHandle, int trackIndex);

    /// <summary>
    /// Get the track index for an audio source.
    /// </summary>
    int GetSourceTrack(ulong sourceHandle);

    /// <summary>
    /// Set volume for an audio source (0.0 = mute, 1.0 = full).
    /// </summary>
    void SetSourceVolume(ulong sourceHandle, float volume);

    /// <summary>
    /// Get volume for an audio source.
    /// </summary>
    float GetSourceVolume(ulong sourceHandle);

    /// <summary>
    /// Mute or unmute an audio source.
    /// </summary>
    void SetSourceMuted(ulong sourceHandle, bool muted);

    /// <summary>
    /// Check if an audio source is muted.
    /// </summary>
    bool IsSourceMuted(ulong sourceHandle);

    /// <summary>
    /// Set the name for an audio track.
    /// </summary>
    void SetTrackName(int trackIndex, string name);

    /// <summary>
    /// Enable or disable mixed mode (all sources to one track).
    /// </summary>
    void SetMixedMode(bool mixedMode);
}
```

#### 5.2 P/Invoke Wrapper

**File:** `src/CaptureTool.Domains.Capture.Implementations.Windows/CaptureInterop.cs`

Add P/Invoke declarations:
```csharp
[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern void SetAudioSourceTrack(ulong sourceHandle, int trackIndex);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern int GetAudioSourceTrack(ulong sourceHandle);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern void SetAudioSourceVolume(ulong sourceHandle, float volume);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern float GetAudioSourceVolume(ulong sourceHandle);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern void SetAudioSourceMuted(ulong sourceHandle, bool muted);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal static extern bool GetAudioSourceMuted(ulong sourceHandle);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
internal static extern void SetAudioTrackName(int trackIndex, string name);

[DllImport("CaptureInterop.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern void SetAudioMixingMode(bool mixedMode);
```

#### 5.3 Implementation

**File:** `src/CaptureTool.Domains.Capture.Implementations.Windows/WindowsAudioMixerConfiguration.cs`

Implement IAudioMixerConfiguration using P/Invoke calls.

**Acceptance Criteria:**
- [ ] C# layer can configure audio routing
- [ ] C# layer can set per-source volume
- [ ] C# layer can mute/unmute sources
- [ ] C# layer can set track names
- [ ] P/Invoke marshaling works correctly
- [ ] No memory leaks in interop layer

---

### Task 6: Testing and Documentation (Week 4-5)

**Goal:** Comprehensive testing and documentation of Phase 3 features.

#### 6.1 Unit Tests

Create tests for:
- AudioMixer sample rate conversion
- AudioMixer volume scaling
- AudioMixer mixing algorithm
- MP4SinkWriter multi-track initialization
- Audio routing configuration

#### 6.2 Integration Tests

Test scenarios:
1. **Desktop + Microphone:** Record both to separate tracks
2. **Mixed Mode:** Record both sources mixed to one track
3. **Volume Control:** Verify volume changes during recording
4. **Mute/Unmute:** Verify muting works without artifacts
5. **Format Mismatch:** Test 44.1kHz + 48kHz sources together
6. **Professional Tool Import:** Import MP4 in Premiere/DaVinci, verify tracks

#### 6.3 Performance Tests

Measure:
- CPU usage with 2, 3, 4, 5, 6 sources
- Memory usage (buffer allocations)
- Latency (audio callback to MP4 write)
- Real-time factor (1.0 = keeps up, <1.0 = can't keep up)

#### 6.4 Documentation

Update:
- `src/CaptureInterop/README.md` - Add AudioMixer architecture
- `docs/Phase-3-Completion-Summary.md` - Create completion summary
- API documentation for new C# interfaces

**Acceptance Criteria:**
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Performance meets criteria (<5% CPU overhead)
- [ ] Documentation complete
- [ ] MP4 files verified in professional tools

---

## Implementation Timeline

### Week 1: AudioMixer Core

**Days 1-2:**
- [ ] Create AudioMixer.h and AudioMixer.cpp
- [ ] Implement Initialize(), AddSource(), RemoveSource()
- [ ] Implement volume and mute control
- [ ] Basic mixing algorithm (float conversion + addition)

**Days 3-4:**
- [ ] Implement sample rate conversion using Media Foundation
- [ ] Implement format normalization (mono↔stereo, bit depth conversion)
- [ ] Add ring buffer for temporal alignment
- [ ] Implement ProcessAudioSamples()

**Day 5:**
- [ ] Unit tests for AudioMixer
- [ ] Performance profiling
- [ ] Bug fixes and optimization

### Week 2: MP4SinkWriter Multi-Track

**Days 1-2:**
- [ ] Extend MP4SinkWriter.h with multi-track APIs
- [ ] Update InitializeAudioStream() for track index
- [ ] Add internal track management (arrays, indices)

**Days 3-4:**
- [ ] Update WriteAudioSample() for track routing
- [ ] Test multi-track initialization
- [ ] Verify MP4 files with multiple audio streams

**Day 5:**
- [ ] Backward compatibility testing (single track mode)
- [ ] MP4 metadata (track names)
- [ ] Bug fixes

### Week 3: Routing and Integration

**Days 1-2:**
- [ ] Implement AudioRoutingConfig class
- [ ] Add C++ exports for configuration
- [ ] Unit tests for routing config

**Days 3-5:**
- [ ] Integrate AudioMixer with ScreenRecorder
- [ ] Wire up source callbacks
- [ ] Wire up mixer output callbacks
- [ ] Test desktop + microphone recording
- [ ] Verify multi-track MP4 output

### Week 4: C# Layer and Testing

**Days 1-2:**
- [ ] Create C# interfaces (IAudioMixerConfiguration)
- [ ] Implement P/Invoke wrapper
- [ ] Implement WindowsAudioMixerConfiguration

**Days 3-4:**
- [ ] Integration tests (desktop + mic scenarios)
- [ ] Performance tests (CPU, memory, latency)
- [ ] Professional tool verification (Premiere, DaVinci)

**Day 5:**
- [ ] Documentation
- [ ] Bug fixes
- [ ] Final validation

### Week 5 (Optional Buffer):
- [ ] Additional testing
- [ ] Performance optimization
- [ ] Edge case fixes
- [ ] Documentation polish

---

## Testing Strategy

### Unit Tests

**AudioMixer Tests:**
```cpp
TEST(AudioMixerTest, InitializeWithTargetFormat)
TEST(AudioMixerTest, AddRemoveSource)
TEST(AudioMixerTest, VolumeControl)
TEST(AudioMixerTest, MuteUnmute)
TEST(AudioMixerTest, SampleRateConversion44to48)
TEST(AudioMixerTest, SampleRateConversion48to44)
TEST(AudioMixerTest, MixTwoSources)
TEST(AudioMixerTest, MixWithVolume)
TEST(AudioMixerTest, ClippingPrevention)
```

**MP4SinkWriter Multi-Track Tests:**
```cpp
TEST(MP4SinkWriterTest, InitializeMultipleAudioStreams)
TEST(MP4SinkWriterTest, WriteToTrack0)
TEST(MP4SinkWriterTest, WriteToTrack1)
TEST(MP4SinkWriterTest, WriteToMultipleTracks)
TEST(MP4SinkWriterTest, TrackMetadata)
```

### Integration Tests

**Scenario 1: Desktop + Microphone (Separate Tracks)**
- Expected: MP4 with 2 audio tracks
- Track 0: Desktop audio
- Track 1: Microphone audio
- Both tracks in sync with video

**Scenario 2: Desktop + Microphone (Mixed)**
- Expected: MP4 with 1 audio track
- Track 0: Desktop + microphone mixed
- Single track in sync with video

**Scenario 3: Volume Control**
- Expected: Audio level changes during recording
- Microphone at 50% volume
- Desktop at 100% volume
- No artifacts at volume change points

**Scenario 4: Format Mismatch**
- Expected: Successful recording with mismatched formats
- Desktop: 48kHz, stereo
- Microphone: 44.1kHz, mono
- AudioMixer handles conversion

**Scenario 5: Professional Tool Import**
- Expected: MP4 imports into Adobe Premiere Pro
- Expected: MP4 imports into DaVinci Resolve
- Expected: Both tracks visible and editable
- Expected: Track names displayed correctly

### Performance Tests

**CPU Usage Test:**
```
Baseline: Video-only recording (no audio) = X%
1 audio source: X + Y%
2 audio sources: X + Z%
Target: Z < 5% (additional overhead per source < 5%)
```

**Memory Usage Test:**
```
Baseline: Video-only recording = A MB
1 audio source: A + B MB
2 audio sources: A + C MB
Target: C < 50 MB (per-source overhead < 50 MB)
```

**Latency Test:**
```
Measure time from audio source callback to MP4SinkWriter write
Target: < 10ms (real-time processing)
```

**Real-Time Factor Test:**
```
Record for 60 seconds
Measure actual elapsed time
Target: Real-time factor ≥ 1.0 (keeps up with capture)
```

### Regression Tests

Ensure Phase 1 and Phase 2 functionality still works:
- [ ] Video-only recording (no audio)
- [ ] Video + desktop audio (legacy mode)
- [ ] Video + desktop audio (source-based mode)
- [ ] All Phase 2 audio sources still functional

---

## Risk Mitigation

### Risk 1: Audio Synchronization Across Multiple Sources

**Impact:** High  
**Probability:** Medium

**Symptoms:**
- Audio drift between tracks
- Tracks not aligned with video
- Increasing delay over long recordings

**Mitigation:**
1. **Common Time Base:** Use QPC timestamps consistently
2. **Timestamp Accumulation:** Maintain proper timestamp continuity per source
3. **Ring Buffer Alignment:** Align sources temporally before mixing
4. **Regular Validation:** Check A/V sync at 1-second intervals

**Contingency:**
- Implement timestamp adjustment algorithm
- Add sync correction every N seconds
- Fall back to mixed mode if separate tracks show drift

### Risk 2: Sample Rate Conversion Quality

**Impact:** Medium  
**Probability:** Medium

**Symptoms:**
- Audio artifacts (aliasing, ringing)
- Reduced audio quality
- High CPU usage from SRC

**Mitigation:**
1. **Use Media Foundation Resampler:** Proven, hardware-accelerated
2. **Quality Settings:** Configure SRC quality (60 is good balance)
3. **Pre-Filtering:** Low-pass filter before downsampling
4. **Testing:** Test all common sample rate combinations

**Contingency:**
- Require all sources to use same sample rate (48kHz)
- Document sample rate requirements
- Provide clear error messages for unsupported rates

### Risk 3: MP4 Container Multi-Track Support

**Impact:** High  
**Probability:** Low

**Symptoms:**
- MP4 files don't open in media players
- Only first audio track plays
- Track metadata missing

**Mitigation:**
1. **Research:** Verify MP4 multi-track support in Media Foundation
2. **Testing:** Test with multiple media players (VLC, Windows Media Player, web browsers)
3. **Professional Tools:** Verify with Premiere Pro, DaVinci Resolve
4. **Alternative:** Consider MKV container if MP4 proves problematic

**Contingency:**
- Implement MKV muxing as alternative
- Document which tools support multi-track MP4
- Provide export tool to extract tracks

### Risk 4: Real-Time Performance

**Impact:** High  
**Probability:** Medium

**Symptoms:**
- CPU usage too high (>10% overhead)
- Dropped audio samples
- Recording can't keep up in real-time

**Mitigation:**
1. **Profiling:** Profile early and often
2. **Optimization:** Optimize hot paths (mixing loop, SRC)
3. **SIMD:** Use SSE/AVX for mixing if needed
4. **Threading:** Ensure audio processing doesn't block capture threads

**Contingency:**
- Reduce audio track count (limit to 4 instead of 6)
- Provide "performance mode" with reduced quality
- Document system requirements

### Risk 5: Memory Management in Multi-Track Pipeline

**Impact:** Medium  
**Probability:** Low

**Symptoms:**
- Memory leaks
- Increasing memory usage during recording
- Out-of-memory crashes on long recordings

**Mitigation:**
1. **RAII:** Use smart pointers (wil::com_ptr, std::unique_ptr)
2. **Buffer Pools:** Reuse audio buffers instead of constant allocation
3. **Leak Detection:** Use Application Verifier, AddressSanitizer
4. **Testing:** Long-duration recording tests (1+ hour)

**Contingency:**
- Implement buffer limits (max queue size)
- Add memory monitoring and warnings
- Document recommended recording durations

---

## Success Criteria

### Functional Requirements

- [ ] **Multi-Source Mixing:** AudioMixer successfully combines multiple audio sources
- [ ] **Multi-Track Recording:** MP4 files contain up to 6 separate audio tracks
- [ ] **Volume Control:** Per-source volume control works (0.0 - 1.0 range)
- [ ] **Mute/Unmute:** Per-source mute/unmute works without artifacts
- [ ] **Sample Rate Conversion:** Handles 44.1kHz, 48kHz, 96kHz sources
- [ ] **Format Normalization:** Handles mono/stereo, 16-bit/32-bit sources
- [ ] **Mixed Mode:** Can record all sources mixed to single track
- [ ] **Separate Track Mode:** Can record each source to separate track
- [ ] **Track Names:** Track metadata visible in professional tools
- [ ] **A/V Sync:** Audio tracks stay in sync with video (< 50ms drift)

### Performance Requirements

- [ ] **CPU Usage:** < 5% additional CPU per audio source (on i5-8600K or better)
- [ ] **Memory Usage:** < 50 MB additional memory per audio source
- [ ] **Latency:** < 10ms from audio capture to MP4 write
- [ ] **Real-Time Factor:** ≥ 1.0 (keeps up with real-time capture)
- [ ] **Long Recording:** Can record for 1+ hour without degradation

### Quality Requirements

- [ ] **No Artifacts:** No clicks, pops, or distortion in mixed audio
- [ ] **No Clipping:** Proper gain control prevents clipping
- [ ] **No Drift:** Multi-track audio stays aligned (< 50ms drift over 10 minutes)
- [ ] **Professional Tool Support:** MP4 files import correctly into:
  - [ ] Adobe Premiere Pro
  - [ ] DaVinci Resolve
  - [ ] Audacity (audio tracks)
  - [ ] VLC Media Player

### Code Quality Requirements

- [ ] **Unit Tests:** All AudioMixer functions have unit tests
- [ ] **Integration Tests:** All recording scenarios have integration tests
- [ ] **Documentation:** API documentation complete for all public interfaces
- [ ] **No Memory Leaks:** Valgrind/AddressSanitizer clean
- [ ] **Thread Safe:** All shared state properly protected with mutexes
- [ ] **Error Handling:** All failure paths return proper error codes

### Backward Compatibility Requirements

- [ ] **Legacy Mode:** Single-track recording still works (Phase 1/2 functionality)
- [ ] **Existing APIs:** All existing C# APIs still functional
- [ ] **Default Behavior:** Default behavior matches Phase 2 (desktop audio to Track 0)

---

## Appendices

### Appendix A: Sample Rate Conversion (SRC) Reference

**Common Sample Rates:**
- 44100 Hz (CD quality)
- 48000 Hz (professional audio/video)
- 96000 Hz (high-definition audio)

**Media Foundation Resampler:**
- CLSID: `CLSID_CResamplerMediaObject`
- Quality setting: 60 (good balance of quality and performance)
- Supports all common sample rates
- Hardware accelerated on modern CPUs

**SRC Algorithm:**
```cpp
// Pseudocode for Media Foundation SRC
IMFTransform* resampler;
CoCreateInstance(CLSID_CResamplerMediaObject, &resampler);

// Configure input type
IMFMediaType* inputType;
MFCreateMediaType(&inputType);
inputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
inputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float);
inputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, inputSampleRate);
resampler->SetInputType(0, inputType, 0);

// Configure output type
IMFMediaType* outputType;
MFCreateMediaType(&outputType);
outputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
outputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float);
outputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, outputSampleRate);
resampler->SetOutputType(0, outputType, 0);

// Process samples
IMFSample* inputSample = CreateSampleFromBuffer(inputData, inputFrames);
resampler->ProcessInput(0, inputSample, 0);
resampler->ProcessOutput(0, outputBuffer, &status);
```

### Appendix B: Audio Mixing Algorithm

**Float Mixing (Recommended):**
```cpp
void AudioMixer::MixSamples(const std::vector<const BYTE*>& sources,
                             const std::vector<UINT32>& frameCounts,
                             const std::vector<float>& volumes,
                             BYTE* output, UINT32 outputFrames)
{
    // Convert all sources to float
    std::vector<std::vector<float>> floatSources;
    for (size_t i = 0; i < sources.size(); i++)
    {
        std::vector<float> floatBuffer(frameCounts[i] * m_channels);
        ConvertToFloat(sources[i], floatBuffer.data(), frameCounts[i], format);
        floatSources.push_back(std::move(floatBuffer));
    }

    // Mix
    std::vector<float> mixBuffer(outputFrames * m_channels, 0.0f);
    for (size_t i = 0; i < floatSources.size(); i++)
    {
        ApplyVolume(floatSources[i].data(), floatSources[i].size(), volumes[i]);
        for (size_t j = 0; j < floatSources[i].size(); j++)
        {
            mixBuffer[j] += floatSources[i][j];
        }
    }

    // Clipping prevention (soft limiter)
    for (size_t i = 0; i < mixBuffer.size(); i++)
    {
        if (mixBuffer[i] > 1.0f) mixBuffer[i] = 1.0f;
        if (mixBuffer[i] < -1.0f) mixBuffer[i] = -1.0f;
    }

    // Convert back to output format
    ConvertFromFloat(mixBuffer.data(), output, outputFrames);
}
```

**Clipping Prevention Strategies:**
1. **Hard Limiter:** Clamp to ±1.0 (simple, can cause distortion)
2. **Soft Limiter:** Smooth curve near ±1.0 (better quality)
3. **Compressor:** Reduce dynamic range (professional quality)
4. **Auto-Gain:** Adjust overall volume to prevent clipping

### Appendix C: MP4 Multi-Track Structure

**MP4 Atom Structure:**
```
ftyp (file type)
moov (movie container)
  ├─ mvhd (movie header)
  ├─ trak (video track)
  │   ├─ tkhd (track header)
  │   ├─ mdia (media)
  │   │   ├─ mdhd (media header)
  │   │   ├─ hdlr (handler: vide)
  │   │   └─ minf (media info)
  │   └─ ...
  ├─ trak (audio track 1)
  │   ├─ tkhd (track header)
  │   ├─ mdia (media)
  │   │   ├─ mdhd (media header)
  │   │   ├─ hdlr (handler: soun)
  │   │   └─ minf (media info)
  │   └─ ...
  ├─ trak (audio track 2)
  │   └─ ...
  └─ ...
mdat (media data)
```

**Media Foundation Multi-Track:**
- Each `AddStream()` call adds a new track
- Track type determined by media type (video/audio)
- Track metadata set via attributes
- Tracks interleaved in mdat section

### Appendix D: Testing Checklist

**Pre-Implementation:**
- [ ] Review Phase 3 plan with team
- [ ] Set up test environment (Premiere, DaVinci, VLC)
- [ ] Prepare test audio sources (44.1kHz, 48kHz files)

**During Implementation:**
- [ ] Run unit tests after each function
- [ ] Profile hot paths for performance
- [ ] Test on multiple machines (different CPUs, GPUs)

**Post-Implementation:**
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Performance tests meet criteria
- [ ] Professional tool verification complete
- [ ] Documentation reviewed
- [ ] Code review complete

**Release Checklist:**
- [ ] Phase 3 complete
- [ ] No known critical bugs
- [ ] Performance acceptable
- [ ] Documentation published
- [ ] Release notes prepared

---

## Summary

Phase 3 is the most complex phase so far, implementing real-time audio mixing, multi-track recording, and advanced audio routing. The 4-5 week timeline provides adequate time for implementation, testing, and optimization. Success criteria are clearly defined, and risks are identified with mitigation strategies.

**Key Milestones:**
- Week 1: AudioMixer core functional
- Week 2: Multi-track MP4 recording working
- Week 3: Full integration with ScreenRecorder
- Week 4: C# layer complete, all tests passing
- Week 5: Buffer for polish and optimization

**Next Phase:** Phase 4 will focus on advanced muxing, separate encoder interfaces, and codec options (H.265, AV1).

---

**Document Version:** 1.0  
**Last Updated:** 2025-12-18  
**Author:** GitHub Copilot  
**Status:** Ready for Implementation
