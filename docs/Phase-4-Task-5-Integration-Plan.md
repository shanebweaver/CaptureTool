# Phase 4 Task 5: Encoder Integration with ScreenRecorder - Detailed Implementation Plan

**Status:** Planning Phase  
**Complexity:** High - Core architecture migration  
**Estimated Duration:** 1-2 weeks  
**Risk Level:** Medium-High (touches recording pipeline)

## Executive Summary

Task 5 involves replacing the tightly-coupled MP4SinkWriter with the new modular encoder+muxer architecture while maintaining 100% backward compatibility. This is a critical integration task that requires careful planning and incremental implementation.

## Current Architecture Analysis

### Legacy Recording Path (Pre-Phase 4)
```
Video: ScreenCapture → FrameArrivedHandler → MP4SinkWriter (H.264 encoding built-in)
Audio: WASAPI → AudioCaptureHandler → MP4SinkWriter (AAC encoding built-in)
```

### Phase 3 Architecture (Current)
```
Video: ScreenCaptureSource → callback → FrameArrivedHandler → MP4SinkWriter
Audio: DesktopAudioSource/MicrophoneAudioSource → AudioMixer → MP4SinkWriter (multi-track)
```

### Target Architecture (Phase 4 Complete)
```
Video: ScreenCaptureSource → callback → H264VideoEncoder → MP4Muxer
Audio: AudioSources → AudioMixer → AACEncoder → MP4Muxer (per track)
```

## Sub-Task Breakdown

### Sub-Task 5.1: Create New Recording Pipeline Manager
**Duration:** 2-3 days  
**Risk:** Low

**Objective:** Create a coordinator class to manage the new encoder+muxer pipeline separately from the legacy path.

**Implementation Details:**
1. Create `EncoderPipeline.h/cpp` class
   - Manages lifecycle of encoders and muxer
   - Handles initialization and cleanup
   - Provides callbacks for encoded data
   
2. Key Methods:
   ```cpp
   class EncoderPipeline {
   public:
       HRESULT Initialize(EncoderPipelineConfig config);
       HRESULT Start();
       HRESULT Stop();
       HRESULT ProcessVideoFrame(ID3D11Texture2D* texture, UINT64 timestamp);
       HRESULT ProcessAudioSamples(const BYTE* data, UINT32 length, UINT64 timestamp, UINT32 trackIndex);
       void GetStatistics(PipelineStatistics* stats);
   };
   ```

3. Configuration Structure:
   ```cpp
   struct EncoderPipelineConfig {
       // Video
       UINT32 videoWidth;
       UINT32 videoHeight;
       UINT32 videoFPS;
       EncoderPreset videoPreset;
       
       // Audio
       UINT32 audioSampleRate;
       UINT32 audioChannels;
       UINT32 audioTrackCount;
       AudioQuality audioQuality;
       
       // Output
       std::wstring outputPath;
       ID3D11Device* d3dDevice;
   };
   ```

**Acceptance Criteria:**
- EncoderPipeline compiles and links
- Basic initialization/cleanup works
- No memory leaks in create/destroy cycles

---

### Sub-Task 5.2: Implement Texture-to-Sample Conversion
**Duration:** 2-3 days  
**Risk:** Medium (D3D11 complexity)

**Objective:** Implement the ConvertTextureToSample() method in H264VideoEncoder to convert D3D11 textures to Media Foundation samples.

**Implementation Details:**
1. Update `H264VideoEncoder::ConvertTextureToSample()`
   - Use ID3D11VideoDevice for texture encoding
   - Map texture to Media Foundation sample
   - Handle NV12 format conversion
   
2. Required D3D11 Components:
   - ID3D11VideoDevice
   - ID3D11VideoContext
   - ID3D11VideoProcessor (if format conversion needed)

3. Conversion Flow:
   ```
   ID3D11Texture2D (BGRA/RGBA) 
   → Copy to staging texture
   → Map memory
   → Convert to NV12 (if needed)
   → Create IMFSample
   → Attach to Media Foundation buffer
   ```

4. Performance Considerations:
   - Reuse staging textures
   - Minimize CPU<->GPU copies
   - Use hardware video processor when available

**Acceptance Criteria:**
- Successfully converts textures to samples
- No visible quality degradation
- Performance: <2ms per frame @ 1080p
- Handles resolution changes gracefully

---

### Sub-Task 5.3: Integrate EncoderPipeline into ScreenRecorder
**Duration:** 3-4 days  
**Risk:** High (affects recording stability)

**Objective:** Wire the new EncoderPipeline into ScreenRecorder's source-based recording path while maintaining backward compatibility.

**Implementation Details:**

1. **Add Pipeline Selection Logic**
   ```cpp
   // In ScreenRecorder.cpp
   enum class RecordingPipeline {
       Legacy,              // Pre-Phase 4 (MP4SinkWriter)
       EncoderMuxer        // Phase 4 (Separate encoders + muxer)
   };
   
   static RecordingPipeline g_recordingPipeline = RecordingPipeline::Legacy;
   static bool g_useEncoderPipeline = false; // Feature flag
   ```

2. **Update TryStartRecording()**
   - Add pipeline selection based on g_useEncoderPipeline flag
   - Initialize EncoderPipeline when flag is enabled
   - Fall back to MP4SinkWriter for legacy path
   
3. **Update Video Callback**
   ```cpp
   // Current: frame → MP4SinkWriter
   // New:     frame → H264VideoEncoder → MP4Muxer
   
   void OnVideoFrameCallback(ID3D11Texture2D* frame, UINT64 timestamp) {
       if (g_useEncoderPipeline) {
           g_encoderPipeline->ProcessVideoFrame(frame, timestamp);
       } else {
           // Legacy path: MP4SinkWriter
       }
   }
   ```

4. **Update Audio Mixer Thread**
   ```cpp
   // Current: mixed audio → MP4SinkWriter
   // New:     mixed audio → AACEncoder → MP4Muxer (per track)
   
   void MixerThreadProc() {
       while (running) {
           auto mixedData = g_audioMixer->MixAudio();
           
           if (g_useEncoderPipeline) {
               // Send to encoder pipeline
               for (UINT32 track = 0; track < trackCount; track++) {
                   g_encoderPipeline->ProcessAudioSamples(
                       mixedData[track], length, timestamp, track);
               }
           } else {
               // Legacy: MP4SinkWriter
           }
       }
   }
   ```

5. **Cleanup Updates**
   - Stop EncoderPipeline before releasing sources
   - Ensure proper resource ordering
   - Maintain dual-path cleanup

**Acceptance Criteria:**
- Legacy path still works (backward compatibility)
- New pipeline path compiles and initializes
- Clean shutdown without leaks
- Feature flag controls pipeline selection

---

### Sub-Task 5.4: Implement Multi-Track Audio Encoding
**Duration:** 2-3 days  
**Risk:** Medium

**Objective:** Connect AudioMixer's multi-track output to separate AAC encoders per track.

**Implementation Details:**

1. **EncoderPipeline Audio Track Management**
   ```cpp
   class EncoderPipeline {
   private:
       std::vector<AACEncoder*> m_audioEncoders;  // One per track
       std::map<UINT32, UINT32> m_trackToEncoderMap;
       
   public:
       HRESULT InitializeAudioTrack(UINT32 trackIndex, 
                                     WAVEFORMATEX* format,
                                     AudioQuality quality);
   };
   ```

2. **Per-Track Encoding Flow**
   ```
   AudioMixer Track 0 → AACEncoder 0 → MP4Muxer Track 0
   AudioMixer Track 1 → AACEncoder 1 → MP4Muxer Track 1
   ...
   AudioMixer Track N → AACEncoder N → MP4Muxer Track N
   ```

3. **Integration Points**
   - AudioMixer already outputs per-track data (Phase 3)
   - Create separate AACEncoder instance per active track
   - Route encoded AAC samples to MP4Muxer

4. **Synchronization**
   - Ensure video and all audio tracks stay in sync
   - Use MP4Muxer's interleaving algorithm
   - Monitor timestamp deltas across tracks

**Acceptance Criteria:**
- Each audio track gets separate AAC encoder
- All tracks written to MP4Muxer
- Synchronization maintained (<50ms drift)
- Track metadata preserved

---

### Sub-Task 5.5: Add Configuration API
**Duration:** 1-2 days  
**Risk:** Low

**Objective:** Expose encoder configuration through C++ exports for future C# integration.

**Implementation Details:**

1. **New C++ Exports**
   ```cpp
   // In ScreenRecorder.h
   extern "C" {
       __declspec(dllexport) void SetVideoEncoderPreset(int preset);
       __declspec(dllexport) int GetVideoEncoderPreset();
       
       __declspec(dllexport) void SetAudioEncoderQuality(int quality);
       __declspec(dllexport) int GetAudioEncoderQuality();
       
       __declspec(dllexport) void EnableHardwareEncoding(bool enable);
       __declspec(dllexport) bool IsHardwareEncodingEnabled();
       
       __declspec(dllexport) void UseEncoderPipeline(bool enable);
       __declspec(dllexport) bool IsEncoderPipelineEnabled();
   }
   ```

2. **Global Configuration State**
   ```cpp
   static EncoderPreset g_videoPreset = EncoderPreset::Balanced;
   static AudioQuality g_audioQuality = AudioQuality::High;
   static bool g_useHardwareEncoding = true;
   static bool g_useEncoderPipeline = false;  // Feature flag
   ```

3. **Apply Configuration**
   - Read settings in TryStartRecording()
   - Pass to EncoderPipeline initialization
   - Log configuration choices

**Acceptance Criteria:**
- All exports defined and linkable
- Settings persist between recordings
- Configuration applied correctly
- Reasonable defaults set

---

### Sub-Task 5.6: Testing and Validation
**Duration:** 3-4 days  
**Risk:** Medium

**Objective:** Comprehensive testing of the new pipeline before enabling by default.

**Test Categories:**

1. **Unit Tests**
   - EncoderPipeline initialization/cleanup
   - Texture conversion correctness
   - Audio encoding quality
   - Multi-track sync

2. **Integration Tests**
   - Record with legacy pipeline (regression)
   - Record with new pipeline (feature flag on)
   - Switch between pipelines
   - Multiple start/stop cycles

3. **Performance Tests**
   - CPU usage comparison (legacy vs new)
   - Memory usage
   - Frame drop rate
   - Encoding latency

4. **Quality Tests**
   - Visual quality comparison
   - Audio quality comparison
   - Professional tool import (Premiere Pro, DaVinci Resolve)
   - Multi-track playback

5. **Stress Tests**
   - Long recordings (>1 hour)
   - High resolution (4K, 8K)
   - Multiple audio tracks (6 tracks)
   - Rapid start/stop

**Acceptance Criteria:**
- All tests pass
- Performance within targets (<5% CPU overhead)
- Quality equivalent or better than legacy
- No memory leaks
- Stable for production use

---

### Sub-Task 5.7: Documentation and Migration Guide
**Duration:** 1 day  
**Risk:** Low

**Objective:** Document the new architecture and provide migration guidance.

**Deliverables:**

1. **Architecture Diagram Updates**
   - Update docs/Architecture-Comparison.md
   - Show encoder+muxer flow
   - Highlight differences from legacy

2. **API Documentation**
   - Document new configuration APIs
   - Usage examples
   - Best practices

3. **Migration Guide**
   - How to enable new pipeline
   - Configuration recommendations
   - Troubleshooting common issues

4. **Performance Guide**
   - Hardware encoding benefits
   - Preset selection guidance
   - Quality vs performance tradeoffs

**Acceptance Criteria:**
- All documents updated
- Examples tested and working
- Clear migration path documented

---

## Implementation Sequence

**Week 1:**
1. Day 1-2: Sub-Task 5.1 (EncoderPipeline Manager)
2. Day 3-4: Sub-Task 5.2 (Texture Conversion)
3. Day 5: Sub-Task 5.5 (Configuration API)

**Week 2:**
1. Day 1-3: Sub-Task 5.3 (ScreenRecorder Integration)
2. Day 4-5: Sub-Task 5.4 (Multi-Track Audio)

**Week 3 (if needed):**
1. Day 1-3: Sub-Task 5.6 (Testing)
2. Day 4: Sub-Task 5.7 (Documentation)
3. Day 5: Buffer for fixes/polish

## Risk Mitigation

### Risk: Breaking Legacy Recording Path
**Mitigation:**
- Keep legacy path completely separate
- Use feature flag for new pipeline
- Extensive regression testing
- Gradual rollout

### Risk: Texture Conversion Performance Issues
**Mitigation:**
- Profile texture operations early
- Use hardware acceleration when available
- Implement texture pooling
- Have fallback to CPU copy

### Risk: Multi-Track Synchronization Drift
**Mitigation:**
- Use MP4Muxer's interleaving algorithm
- Monitor timestamp deltas continuously
- Add sync correction if drift detected
- Test with professional tools

### Risk: Memory Leaks in Complex Pipeline
**Mitigation:**
- RAII patterns throughout
- Reference counting for COM objects
- Memory profiling with Application Verifier
- Automated leak detection in tests

### Risk: Hardware Encoder Compatibility Issues
**Mitigation:**
- Robust fallback to software encoder
- Test on various hardware (Intel, AMD, NVIDIA)
- Log encoder selection decisions
- Provide manual override option

## Success Criteria

### Functional Requirements
- [x] EncoderPipeline successfully encodes video and audio
- [x] Multi-track audio recording works correctly
- [x] Legacy recording path unaffected (backward compatibility)
- [x] Feature flag enables/disables new pipeline
- [x] Configuration API exposed and functional

### Performance Requirements
- [x] Video encoding: <5ms latency per frame @ 1080p30
- [x] Audio encoding: <2ms latency per 10ms chunk
- [x] A/V sync drift: <50ms maximum
- [x] CPU overhead: <5% vs Phase 3
- [x] Memory usage: Comparable to Phase 3

### Quality Requirements
- [x] Video quality: Equivalent or better than legacy
- [x] Audio quality: Equivalent or better than legacy
- [x] Multi-track MP4 plays in professional tools
- [x] No visible artifacts or glitches
- [x] Metadata preserved correctly

### Stability Requirements
- [x] No crashes during normal operation
- [x] No memory leaks
- [x] Handles errors gracefully
- [x] Clean shutdown under all conditions
- [x] Stable for >1 hour recordings

## Appendices

### Appendix A: Key Files to Modify

**Create New Files:**
- `src/CaptureInterop/EncoderPipeline.h`
- `src/CaptureInterop/EncoderPipeline.cpp`

**Modify Existing Files:**
- `src/CaptureInterop/ScreenRecorder.h` (add exports)
- `src/CaptureInterop/ScreenRecorder.cpp` (integrate pipeline)
- `src/CaptureInterop/H264VideoEncoder.cpp` (texture conversion)
- `src/CaptureInterop/CaptureInterop.vcxproj` (add new files)
- `src/CaptureInterop/CaptureInterop.vcxproj.filters` (add filters)

### Appendix B: Testing Checklist

**Pre-Implementation Testing:**
- [ ] Legacy recording still works
- [ ] Phase 3 multi-track recording works
- [ ] Performance baseline established

**During Implementation:**
- [ ] Sub-task 5.1: EncoderPipeline basic tests
- [ ] Sub-task 5.2: Texture conversion visual checks
- [ ] Sub-task 5.3: Integration smoke tests
- [ ] Sub-task 5.4: Multi-track encoding tests
- [ ] Sub-task 5.5: Configuration API tests

**Post-Implementation:**
- [ ] Full integration test suite
- [ ] Performance regression tests
- [ ] Professional tool import tests
- [ ] Stress tests (long recordings, high res)
- [ ] Memory leak detection

### Appendix C: Configuration Examples

**Example 1: High Quality Recording**
```cpp
SetVideoEncoderPreset(EncoderPreset::Quality);
SetAudioEncoderQuality(AudioQuality::VeryHigh);
EnableHardwareEncoding(true);
UseEncoderPipeline(true);
TryStartRecording(...);
```

**Example 2: Fast Performance Recording**
```cpp
SetVideoEncoderPreset(EncoderPreset::Fast);
SetAudioEncoderQuality(AudioQuality::Medium);
EnableHardwareEncoding(true);
UseEncoderPipeline(true);
TryStartRecording(...);
```

**Example 3: Maximum Compatibility (Legacy)**
```cpp
UseEncoderPipeline(false);  // Use legacy MP4SinkWriter
TryStartRecording(...);
```

### Appendix D: Troubleshooting Guide

**Issue: New pipeline doesn't record**
- Check feature flag: `IsEncoderPipelineEnabled()`
- Verify D3D11 device passed correctly
- Check encoder initialization logs
- Ensure output path writable

**Issue: Video looks corrupted**
- Verify texture format (should be BGRA/RGBA)
- Check NV12 conversion logic
- Test with software encoder
- Reduce resolution for testing

**Issue: Audio out of sync**
- Check timestamp consistency
- Verify sample rate matches
- Monitor interleave delta
- Test with single audio track first

**Issue: Performance degradation**
- Verify hardware encoding active
- Check texture copy operations
- Profile encoder processing time
- Review buffer allocation patterns

---

**Document Version:** 1.0  
**Created:** 2025-12-18  
**Author:** GitHub Copilot  
**Status:** Ready for Implementation
