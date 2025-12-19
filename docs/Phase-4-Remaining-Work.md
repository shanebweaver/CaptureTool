# Phase 4 Remaining Work - TODO Analysis

**Document Status**: Active Tracking  
**Last Updated**: 2025-12-19  
**Purpose**: Comprehensive inventory of incomplete features and pending implementations

---

## Executive Summary

This document catalogs all remaining work items identified through code analysis (TODO, FIXME, NOTE comments and placeholder implementations). These items represent deferred features, incomplete implementations, and areas requiring future enhancement.

**Total Items**: 10 (6 TODO comments, 3 NOTE comments, 1 placeholder implementation)

---

## Category 1: AudioMixer Implementation Gaps

### 1.1 MixAudio() Source Processing (Phase 3)
**File**: `src/CaptureInterop/AudioMixer.cpp:205`  
**Priority**: HIGH  
**Status**: Deferred to Phase 3 completion

**Current State**:
```cpp
// TODO: In Phase 3, we'll implement actual audio capture and mixing here
// For now, this is a framework that will be filled in during implementation
// ConvertAndMixSource(entry, outputBuffer, outputFrames, timestamp);
```

**Description**:
The `MixAudio()` method currently iterates through audio sources but does not actually process them. The `ConvertAndMixSource()` call is commented out.

**Impact**:
- Mixed mode audio routing is non-functional
- Multiple audio sources cannot be combined into a single track
- Only separate track mode works (via GetSourceAudio)

**Work Required**:
1. Implement source data retrieval from IAudioSource callback pattern
2. Integrate with ConvertAndMixSource() for SRC and volume application
3. Accumulate mixed audio into output buffer
4. Add unit tests for multi-source mixing scenarios

**Estimated Effort**: 2-3 days

---

### 1.2 ConvertAndMixSource() Implementation
**File**: `src/CaptureInterop/AudioMixer.cpp:315`  
**Priority**: HIGH  
**Status**: Stub implementation

**Current State**:
```cpp
UINT32 AudioMixer::ConvertAndMixSource(AudioSourceEntry& entry, BYTE* mixBuffer, UINT32 mixFrames, LONGLONG timestamp)
{
    // TODO: Implement in full Phase 3 implementation
    // This will:
    // 1. Get audio data from the source
    // 2. Apply sample rate conversion if needed (using resampler from m_resamplers)
    // 3. Apply volume
    // 4. Mix into output buffer
    return 0;
}
```

**Description**:
Core mixing logic is stubbed out. This method should coordinate sample rate conversion, volume application, and buffer mixing.

**Impact**:
- No actual audio mixing occurs in mixed mode
- Sample rate conversion is not applied
- Per-source volume control doesn't work in mixed mode

**Work Required**:
1. Implement IAudioSource data retrieval (GetData() or callback mechanism)
2. Apply SRC using IMFTransform resampler from m_resamplers map
3. Call ApplyVolume() on converted samples
4. Call MixBuffers() to accumulate into output buffer
5. Handle format mismatches and buffer size alignment
6. Add comprehensive unit tests

**Dependencies**: Requires IAudioSource callback pattern to be clarified

**Estimated Effort**: 3-4 days

---

### 1.3 GetSourceAudio() Data Retrieval
**File**: `src/CaptureInterop/AudioMixer.cpp:421`  
**Priority**: HIGH  
**Status**: Returns silence (placeholder)

**Current State**:
```cpp
// TODO: Implement audio data retrieval in Phase 3
// The IAudioSource uses a callback pattern rather than direct data access
// For now, return silence as this is a placeholder implementation
ZeroMemory(outputBuffer, outputFrames * m_outputFormat.nBlockAlign);
return 0;
```

**Description**:
`GetSourceAudio()` is used in separate track mode to retrieve audio from individual sources. Currently returns silence instead of actual audio data.

**Impact**:
- Separate track mode is non-functional
- Per-source audio routing doesn't produce actual audio
- Multi-track MP4 files have silent tracks

**Work Required**:
1. Design pull-based API for IAudioSource or buffer caching mechanism
2. Implement sample rate conversion for source
3. Apply per-source volume control
4. Return actual audio data instead of silence
5. Add integration tests with real audio sources

**Design Decision Required**: Should IAudioSource provide pull-based GetData() or should AudioMixer cache callback data?

**Estimated Effort**: 2-3 days (plus design clarification)

---

## Category 2: MP4Muxer Metadata Support

### 2.1 Metadata Writing to Container
**File**: `src/CaptureInterop/MP4Muxer.cpp:319`  
**Priority**: MEDIUM  
**Status**: Metadata stored but not written to file

**Current State**:
```cpp
m_metadata[key] = value;

// TODO: Write metadata to MP4 container using IMFSinkWriter attributes
```

**Description**:
The `SetMetadata()` method stores metadata in a map but doesn't write it to the MP4 file. IMFSinkWriter supports attributes like MF_PD_TITLE, MF_PD_AUTHOR, etc.

**Impact**:
- MP4 files lack title, author, copyright, and other metadata
- Professional tools don't display recording information
- Organizational metadata is lost

**Work Required**:
1. Map string keys to IMFSinkWriter attribute GUIDs
2. Call IMFSinkWriter::SetAttribute() for each metadata entry
3. Support common metadata: title, author, copyright, description, creation date
4. Add metadata validation tests

**References**:
- IMFSinkWriter::SetAttribute()
- MF_PD_TITLE, MF_PD_AUTHOR, MF_PD_COPYRIGHT constants

**Estimated Effort**: 1-2 days

---

### 2.2 Video Track Name Metadata
**File**: `src/CaptureInterop/MP4Muxer.cpp:502`  
**Priority**: LOW  
**Status**: Track names not written to MP4

**Current State**:
```cpp
// Set track name as metadata if provided
if (!trackInfo.name.empty())
{
    // TODO: Set track name via IMFSinkWriter attributes
}
```

**Description**:
Video track names are accepted but not written to the MP4 container. Professional tools use track names for organization.

**Impact**:
- Video tracks lack descriptive names
- Reduced usability in professional editing tools
- Users can't distinguish multiple video tracks (future enhancement)

**Work Required**:
1. Research IMFSinkWriter stream-level attribute support
2. Investigate MF_SD_STREAM_NAME or custom MP4 atom
3. Implement attribute setting for video streams
4. Test with Premiere Pro, DaVinci Resolve, Final Cut Pro

**Note**: May require custom MP4 atom writing if IMFSinkWriter doesn't support stream names

**Estimated Effort**: 2-3 days (research + implementation)

---

### 2.3 Audio Track Name Metadata
**File**: `src/CaptureInterop/MP4Muxer.cpp:528`  
**Priority**: MEDIUM  
**Status**: Track names not written to MP4

**Current State**:
```cpp
// Set track name as metadata if provided
if (!trackInfo.name.empty())
{
    // TODO: Set track name via IMFSinkWriter attributes
}
```

**Description**:
Audio track names are accepted but not written to MP4. This is important for multi-track workflows.

**Impact**:
- Users can't identify which audio track is desktop vs microphone
- Professional tools show generic "Audio Track 1", "Audio Track 2" labels
- Workflow efficiency is reduced

**Work Required**:
Same as 2.2 but for audio streams. This is more critical than video since Phase 4 supports up to 6 audio tracks.

**Estimated Effort**: 1-2 days (if 2.2 is completed first)

---

## Category 3: Texture Conversion (Already Resolved)

### 3.1 ~~ConvertTextureToSample() in MP4Muxer~~ ✅
**File**: `src/CaptureInterop/MP4Muxer.cpp:538`  
**Status**: RESOLVED - Implemented in TextureConverter class

**Original Placeholder**:
```cpp
HRESULT MP4Muxer::ConvertTextureToSample(ID3D11Texture2D* pTexture, IMFSample** ppSample)
{
    // This is a placeholder - actual implementation would:
    // 1. Copy texture to staging texture
    // 2. Map staging texture and read pixel data
    // 3. Create IMFMediaBuffer with pixel data
    // 4. Create IMFSample and add buffer
    
    // For now, return not implemented
    // This will be completed during ScreenRecorder integration (Task 5)
    return E_NOTIMPL;
}
```

**Resolution**:
- Implemented in `TextureConverter` class (Phase 4 Task 5.2)
- Uses D3D11 Video Processor for BGRA→NV12 conversion
- Hardware-accelerated GPU-based processing
- Integrated into H264VideoEncoder via ProcessVideoFrame()

**No further work required.**

---

## Category 4: Audio Device Selection

### 4.1 MicrophoneAudioSource Device Selection
**File**: `src/CaptureInterop/MicrophoneAudioSource.cpp:62`  
**Priority**: MEDIUM  
**Status**: Uses default microphone only

**Current State**:
```cpp
// Initialize in capture mode (false = capture endpoint, not loopback)
// TODO Phase 2 Task 2: Add support for specific device ID via m_deviceId
// For now, use default microphone
if (!m_device.Initialize(false, &hr))
{
    return false;
}
```

**Description**:
The class accepts a device ID in the constructor but doesn't use it. Always captures from default microphone.

**Impact**:
- Users cannot select specific microphone
- Multi-microphone setups not supported
- USB microphones might not be captured if not default

**Work Required**:
1. Extend AudioCaptureHandler::Initialize() to accept device ID
2. Use IMMDeviceEnumerator::GetDevice(deviceId) instead of GetDefaultAudioEndpoint()
3. Validate device ID before initialization
4. Add error handling for invalid device IDs
5. Update unit tests to verify device selection

**API Surface**:
```cpp
// Already exists but unused:
MicrophoneAudioSource(const std::wstring& deviceId);
```

**Estimated Effort**: 1-2 days

---

## Category 5: ApplicationAudioSource Per-Process Capture

### 5.1 Full Per-Process Audio Isolation
**File**: `src/CaptureInterop/ApplicationAudioSource.h:15-18`  
**Priority**: LOW (Windows 11 22H2+ only)  
**Status**: Framework exists, full feature deferred

**Current State**:
```cpp
/// NOTE: This is a simplified implementation. Full per-process isolation
/// requires IAudioClient3 with process loopback mode, which is only available
/// on Windows 11 22H2+. For now, this captures all loopback audio but provides
/// the framework for future per-app capture.
```

**Description**:
ApplicationAudioSource accepts a process ID but captures all system audio (loopback). True per-process isolation requires IAudioClient3 with process loopback flags.

**Impact**:
- Cannot isolate audio from specific application
- Captures all system audio regardless of SetProcessId()
- Game/browser/music player audio cannot be separated

**Work Required (Windows 11 22H2+ target)**:
1. Implement IAudioClient3 initialization path
2. Use AUDCLNT_STREAMFLAGS_PROCESS_LOOPBACK flag
3. Set AudioClientProperties.Options = AUDCLNT_STREAMOPTIONS_MATCH_FORMAT
4. Associate process ID with audio session
5. Add Windows version check (build >= 22621)
6. Provide graceful fallback to loopback mode on older Windows

**Technical References**:
- IAudioClient3::Initialize()
- AUDCLNT_STREAMFLAGS_PROCESS_LOOPBACK (Windows 11 22H2+)
- AudioClientProperties structure
- Audio Session API 2.0 documentation

**Estimated Effort**: 4-5 days (research + implementation + testing on Windows 11)

**Note**: This is a nice-to-have feature for Windows 11 users. Current loopback implementation works but captures all audio.

---

### 5.2 SetProcessId() Implementation
**File**: `src/CaptureInterop/ApplicationAudioSource.h:29-31`  
**Priority**: LOW  
**Status**: Accepted but not used

**Current State**:
```cpp
/// Set the process ID to capture audio from.
/// Must be called before Initialize().
/// NOTE: Currently not fully implemented - captures all loopback audio.
/// Full per-process audio requires Windows 11 22H2+ Audio Session API.
void SetProcessId(DWORD processId);
```

**Description**:
Method exists and stores process ID but doesn't affect audio capture behavior. See 5.1 for full context.

**Work Required**:
Same as 5.1 - requires IAudioClient3 implementation.

---

## Category 6: Implementation Notes (Not TODOs)

### 6.1 ApplicationAudioSource Implementation Complexity
**File**: `src/CaptureInterop/ApplicationAudioSource.cpp:114-116`  
**Type**: Informational NOTE  
**Priority**: N/A (documentation only)

**Note Content**:
```cpp
// NOTE: Full per-process capture would require IAudioClient3 with 
// AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_PROCESS_LOOPBACK
// and AudioClientProperties.Options = AUDCLNT_STREAMOPTIONS_MATCH_FORMAT
// This requires Windows 11 22H2+ and is more complex
```

**Description**:
Inline documentation explaining why per-process capture is not implemented. Provides technical guidance for future implementation.

**No action required** - this is documentation for developers.

---

## Summary by Priority

### HIGH Priority (3 items)
Critical for core functionality:
1. **AudioMixer::MixAudio() source processing** - Mixed mode broken
2. **AudioMixer::ConvertAndMixSource()** - No mixing occurs
3. **AudioMixer::GetSourceAudio()** - Separate track mode returns silence

**Total Effort**: 7-10 days  
**Blocking**: Phase 4 audio functionality

---

### MEDIUM Priority (3 items)
Important for professional workflows:
1. **MP4Muxer metadata writing** - Missing file metadata
2. **Audio track name metadata** - Multi-track identification
3. **MicrophoneAudioSource device selection** - User choice of microphone

**Total Effort**: 4-6 days  
**Impact**: Professional tool integration, user flexibility

---

### LOW Priority (3 items + 1 resolved)
Nice-to-have enhancements:
1. **Video track name metadata** - Single video track, less critical
2. **ApplicationAudioSource per-process capture** - Windows 11 22H2+ feature
3. **SetProcessId() implementation** - Requires Windows 11 22H2+
4. ~~**ConvertTextureToSample()**~~ ✅ RESOLVED

**Total Effort**: 7-10 days (includes research)  
**Impact**: Enhanced user experience on Windows 11

---

## Recommended Implementation Order

### Phase 1: Critical Audio Fixes (Week 1)
**Priority**: Unblock Phase 4 core functionality
1. AudioMixer::GetSourceAudio() - 2-3 days
2. AudioMixer::ConvertAndMixSource() - 3-4 days  
   *Includes design decision on IAudioSource data access pattern*

**Milestone**: Both mixed and separate track modes fully functional

---

### Phase 2: Metadata Support (Week 2)
**Priority**: Professional tool integration
1. MP4Muxer::SetMetadata() writing - 1-2 days
2. Audio track name metadata - 1-2 days
3. MicrophoneAudioSource device selection - 1-2 days

**Milestone**: Professional MP4 files with proper metadata

---

### Phase 3: Advanced Features (Week 3)
**Priority**: Enhancement and polish
1. Video track name metadata - 2-3 days
2. AudioMixer::MixAudio() completion - 2-3 days  
   *Integrate ConvertAndMixSource() into mixing loop*

**Milestone**: Complete Phase 3 audio mixer implementation

---

### Phase 4: Windows 11 Features (Future)
**Priority**: Optional enhancement
1. ApplicationAudioSource per-process capture - 4-5 days
   *Research + IAudioClient3 implementation*

**Milestone**: True per-app audio isolation on Windows 11 22H2+

---

## Testing Strategy

Each implementation phase requires corresponding tests:

### Audio Implementation Tests
- Unit tests for AudioMixer methods with mock audio sources
- Integration tests with DesktopAudioSource + MicrophoneAudioSource
- Multi-track recording validation (6 tracks)
- Mixed mode vs separate mode comparison

### Metadata Tests
- Verify metadata written to MP4 container
- Professional tool import tests (Premiere, Resolve, Final Cut)
- Track name display validation
- Special character handling in metadata

### Device Selection Tests
- Enumerate audio devices
- Select non-default microphone
- Handle invalid device IDs
- Graceful fallback on device errors

### Windows 11 Feature Tests
- Version detection (build >= 22621)
- IAudioClient3 availability check
- Per-process audio isolation validation
- Fallback to loopback mode on older Windows

---

## Risk Assessment

### High Risk Items
1. **IAudioSource data access pattern** - Design decision impacts AudioMixer implementation
   - **Mitigation**: Review Phase 3 design doc, clarify callback vs pull pattern
   
2. **IMFSinkWriter metadata support** - May not support all desired attributes
   - **Mitigation**: Research Media Foundation capabilities, consider custom MP4 atoms

3. **Track name metadata** - IMFSinkWriter may not expose stream-level names
   - **Mitigation**: Investigate MP4 atom structure, prepare fallback approach

### Medium Risk Items
1. **Windows 11 per-process audio** - Complex API, limited documentation
   - **Mitigation**: Thorough research, prototype on Windows 11 22H2 VM

2. **Professional tool compatibility** - Each tool handles MP4 differently
   - **Mitigation**: Test with actual tools, gather user feedback

### Low Risk Items
1. **Device selection** - Well-documented WASAPI feature
2. **Basic metadata** - Standard IMFSinkWriter functionality

---

## Conclusion

**Total Remaining Work**: 18-26 days across 10 items (excluding resolved items)

**Critical Path**: AudioMixer audio data retrieval and mixing (HIGH priority, 7-10 days)

**Recommendation**: Focus on HIGH priority audio fixes first to unblock Phase 4 functionality, then address MEDIUM priority metadata support for professional workflows. LOW priority Windows 11 features can be deferred to Phase 5 or future releases.

**Next Steps**:
1. Clarify IAudioSource data access pattern design
2. Implement GetSourceAudio() audio data retrieval
3. Complete ConvertAndMixSource() mixing logic
4. Add comprehensive unit tests for audio pipeline
5. Proceed with metadata support once audio is stable

---

**Document Version**: 1.0  
**Author**: GitHub Copilot  
**Review Status**: Pending team review
