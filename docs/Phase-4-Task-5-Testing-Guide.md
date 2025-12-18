# Phase 4 Task 5.6: Testing and Validation Guide

**Status:** Testing Phase  
**Last Updated:** 2025-12-18  
**Scope:** Comprehensive validation of EncoderPipeline integration

## Executive Summary

This document provides a comprehensive testing and validation plan for Phase 4's encoder abstraction and integration. All tests should pass before enabling the new pipeline by default.

## Test Environment Setup

### Prerequisites
- Windows 10 20H2+ or Windows 11
- Visual Studio 2019/2022
- Media Foundation codecs installed
- Hardware encoder drivers (Intel/AMD/NVIDIA)
- Professional video tools (optional): Adobe Premiere Pro, DaVinci Resolve

### Build Configuration
```bash
# Build in Release mode for performance testing
msbuild CaptureTool.sln /p:Configuration=Release

# Build in Debug mode for memory leak detection
msbuild CaptureTool.sln /p:Configuration=Debug
```

### Test Data Preparation
- Create test output directory: `C:\CaptureTool\TestOutputs`
- Prepare reference recordings for comparison
- Set up performance monitoring tools

---

## 1. Unit Tests

### 1.1 EncoderPipeline Initialization

**Test Procedure:**
1. Create EncoderPipeline instance
2. Call Initialize() with valid configuration
3. Verify HRESULT success
4. Call Stop() and cleanup
5. Verify no memory leaks

**Expected Results:**
- Initialize returns S_OK
- All encoders created successfully
- Muxer initialized correctly
- Clean shutdown without crashes

**Validation:**
```cpp
EncoderPipeline* pipeline = new EncoderPipeline();
EncoderPipelineConfig config = {
    .videoWidth = 1920,
    .videoHeight = 1080,
    .videoFPS = 30,
    .videoPreset = EncoderPreset::Balanced,
    .audioSampleRate = 48000,
    .audioChannels = 2,
    .audioTrackCount = 2,
    .audioQuality = AudioQuality::High,
    .outputPath = L"test.mp4",
    .d3dDevice = d3dDevice
};

HRESULT hr = pipeline->Initialize(config);
assert(SUCCEEDED(hr));

hr = pipeline->Stop();
assert(SUCCEEDED(hr));

delete pipeline; // Should not crash or leak
```

### 1.2 Texture Conversion Correctness

**Test Procedure:**
1. Create test texture with known pattern
2. Convert to NV12 via TextureConverter
3. Validate output format and colors
4. Check for visual artifacts

**Expected Results:**
- Correct NV12 format
- No color shifts
- Sharp edges preserved
- No tearing or artifacts

**Test Patterns:**
- Solid colors (Red, Green, Blue, White, Black)
- Color gradient
- Checkerboard pattern
- Text rendering

### 1.3 AAC Encoder Quality

**Test Procedure:**
1. Encode test audio at different quality levels
2. Measure bitrate accuracy
3. Analyze frequency response
4. Check for audio artifacts

**Expected Results:**
- Bitrate within 5% of target
- Frequency response: 20Hz-20kHz
- No clipping or distortion
- Frame alignment correct (1024 samples)

**Quality Levels:**
- Low: 64 kbps/channel
- Medium: 96 kbps/channel
- High: 128 kbps/channel
- VeryHigh: 192 kbps/channel

### 1.4 Multi-Track Synchronization

**Test Procedure:**
1. Record with desktop audio + microphone
2. Extract each track separately
3. Measure timestamp differences
4. Verify sync accuracy

**Expected Results:**
- Track timestamps aligned
- Drift <50ms over 1 hour
- No dropped samples
- Consistent frame timing

---

## 2. Integration Tests

### 2.1 Legacy Pipeline Regression

**Test Procedure:**
1. Set `UseEncoderPipeline(false)`
2. Record for 60 seconds
3. Verify output matches Phase 3 behavior
4. Compare file size and quality

**Expected Results:**
- No behavioral changes
- Output identical to Phase 3
- Performance unchanged
- No new crashes or issues

### 2.2 New Pipeline Basic Recording

**Test Procedure:**
1. Set `UseEncoderPipeline(true)`
2. Configure preset: Balanced
3. Record for 60 seconds
4. Verify MP4 file created successfully

**Expected Results:**
- File created and playable
- Video and audio in sync
- No dropped frames
- Clean stop

### 2.3 Pipeline Switching

**Test Procedure:**
1. Record with legacy pipeline (30 sec)
2. Stop recording
3. Switch to new pipeline
4. Record again (30 sec)
5. Repeat 5 times

**Expected Results:**
- No crashes on switch
- Both pipelines functional
- Configuration persists
- No resource leaks

### 2.4 Multiple Start/Stop Cycles

**Test Procedure:**
1. Start recording
2. Record for 10 seconds
3. Stop recording
4. Repeat 20 times

**Expected Results:**
- All recordings successful
- No memory accumulation
- Performance stable
- Clean shutdowns

### 2.5 Multi-Track Recording

**Test Procedure:**
1. Enable separate track mode
2. Configure: Desktop Audio → Track 0, Microphone → Track 1
3. Record for 60 seconds
4. Verify both tracks in MP4

**Expected Results:**
- Two audio tracks in MP4
- Track metadata correct
- Independent track levels
- Both tracks in sync

---

## 3. Performance Tests

### 3.1 CPU Usage Comparison

**Test Procedure:**
1. Establish baseline: Phase 3 MP4SinkWriter
2. Measure: Phase 4 Software Encoding
3. Measure: Phase 4 Hardware Encoding
4. Compare CPU usage

**Target Metrics:**
- Hardware encoding: <5% overhead vs Phase 3
- Software encoding: <20% overhead vs Phase 3
- Encoder latency: <5ms per frame @ 1080p30

**Measurement Tools:**
- Windows Performance Monitor
- Task Manager
- Custom performance counters

### 3.2 Memory Usage

**Test Procedure:**
1. Start recording
2. Monitor memory for 30 minutes
3. Check for memory leaks
4. Verify cleanup on stop

**Expected Results:**
- Stable memory footprint
- No memory growth over time
- Peak <500MB for 1080p30
- Complete cleanup on stop

**Tools:**
- Windows Performance Monitor
- Visual Studio Memory Profiler
- Application Verifier

### 3.3 Frame Drop Rate

**Test Procedure:**
1. Record at various resolutions
2. Count frames in output
3. Compare to expected frame count
4. Calculate drop percentage

**Expected Results:**
- 1080p30: 0% drops
- 1440p60: <1% drops
- 4K30: <2% drops (software)
- 4K30: 0% drops (hardware)

### 3.4 Encoding Latency

**Test Procedure:**
1. Timestamp frame capture
2. Timestamp frame encoding
3. Measure delta
4. Calculate average and P95

**Target Latency:**
- Video encoding: <5ms average, <10ms P95
- Audio encoding: <2ms average, <5ms P95
- End-to-end: <50ms (capture to file write)

---

## 4. Quality Tests

### 4.1 Visual Quality Comparison

**Test Procedure:**
1. Record same scene with both pipelines
2. Compare side-by-side
3. Measure PSNR/SSIM
4. Visual inspection

**Test Scenes:**
- Static desktop (text, icons)
- Video playback (motion)
- Game capture (fast motion)
- Screen transitions

**Expected Results:**
- PSNR >35 dB
- SSIM >0.95
- No visible artifacts
- Sharp text rendering

### 4.2 Audio Quality Comparison

**Test Procedure:**
1. Record identical audio with both pipelines
2. Measure frequency response
3. Check for clipping
4. Analyze noise floor

**Expected Results:**
- Frequency response: ±1 dB (20Hz-20kHz)
- Dynamic range: >90 dB
- THD+N: <0.01%
- No audible artifacts

### 4.3 Professional Tool Compatibility

**Test Procedure:**
1. Record multi-track video
2. Import into professional tools
3. Verify track separation
4. Test editing operations

**Tools to Test:**
- Adobe Premiere Pro
- DaVinci Resolve
- Final Cut Pro
- VLC Media Player

**Expected Results:**
- Files import successfully
- All tracks visible
- Track names preserved
- No codec warnings

### 4.4 Codec Compliance

**Test Procedure:**
1. Use MediaInfo to analyze output
2. Verify H.264 High Profile
3. Check AAC-LC compliance
4. Validate MP4 structure

**Expected Output:**
```
Video:
- Format: AVC/H.264
- Profile: High@L4.1
- Frame rate: Constant
- Color space: YUV 4:2:0

Audio:
- Format: AAC-LC
- Channels: 2 or more
- Sample rate: 48000 Hz
- Bitrate: Matches quality setting
```

---

## 5. Stress Tests

### 5.1 Long Recording Duration

**Test Procedure:**
1. Configure new pipeline
2. Start recording
3. Record for 2 hours
4. Monitor system stability

**Expected Results:**
- No crashes
- A/V sync maintained
- Memory stable
- CPU usage consistent
- File size as expected

### 5.2 High Resolution Recording

**Test Procedure:**
1. Record at various resolutions
2. Test both hardware and software encoding
3. Monitor performance

**Resolutions to Test:**
- 1920x1080 @ 30/60 fps
- 2560x1440 @ 30/60 fps
- 3840x2160 @ 30 fps

**Expected Results:**
- All resolutions work
- Hardware encoding preferred
- Software fallback functional
- Quality maintained

### 5.3 Multiple Audio Tracks (Maximum)

**Test Procedure:**
1. Configure 6 separate audio tracks
2. Record for 10 minutes
3. Verify all tracks encoded
4. Check synchronization

**Expected Results:**
- All 6 tracks in MP4
- Tracks independent
- Synchronization maintained
- File plays correctly

### 5.4 Rapid Start/Stop Stress

**Test Procedure:**
1. Start recording
2. Stop after 1 second
3. Repeat 100 times rapidly
4. Check for crashes or leaks

**Expected Results:**
- No crashes
- All files created
- No resource leaks
- Clean state after test

### 5.5 Low Disk Space

**Test Procedure:**
1. Record to nearly full disk
2. Continue until disk full
3. Verify graceful failure
4. Check error handling

**Expected Results:**
- Error returned gracefully
- No data corruption
- Clean shutdown
- Appropriate error message

---

## 6. Hardware Compatibility Tests

### 6.1 Intel Hardware Encoding

**Test Procedure:**
1. Enable hardware encoding
2. Verify Intel Quick Sync detected
3. Record and validate output
4. Compare to software encoding

**Expected Results:**
- Hardware encoder detected
- Faster encoding
- Lower CPU usage
- Quality acceptable

### 6.2 AMD Hardware Encoding

**Test Procedure:**
1. Enable hardware encoding
2. Verify AMD VCE/VCN detected
3. Record and validate output
4. Compare to software encoding

**Expected Results:**
- Hardware encoder detected
- Faster encoding
- Lower CPU usage
- Quality acceptable

### 6.3 NVIDIA Hardware Encoding

**Test Procedure:**
1. Enable hardware encoding
2. Verify NVENC detected
3. Record and validate output
4. Compare to software encoding

**Expected Results:**
- Hardware encoder detected
- Faster encoding
- Lower CPU usage
- Quality acceptable

### 6.4 Software Fallback

**Test Procedure:**
1. Disable hardware encoding
2. Force software encoder
3. Record and validate output
4. Verify functionality

**Expected Results:**
- Software encoder used
- Recording works correctly
- Higher CPU usage expected
- Quality maintained

---

## 7. Error Handling Tests

### 7.1 Invalid Configuration

**Test Procedure:**
1. Try initializing with invalid parameters
2. Test various edge cases
3. Verify error handling

**Test Cases:**
- Width/height = 0
- Invalid codec
- Null D3D device
- Invalid output path
- Sample rate = 0

**Expected Results:**
- Appropriate error returned
- No crash
- Clear error messages
- Clean failure

### 7.2 Encoder Initialization Failure

**Test Procedure:**
1. Simulate encoder creation failure
2. Verify fallback behavior
3. Check error propagation

**Expected Results:**
- Graceful failure
- Fallback attempted
- Error logged
- Clean state

### 7.3 Disk Write Failure

**Test Procedure:**
1. Record to read-only location
2. Verify error handling
3. Check cleanup

**Expected Results:**
- Error returned
- No crash
- Resources cleaned up
- File not corrupted

---

## 8. Configuration API Tests

### 8.1 Preset Switching

**Test Procedure:**
1. Test all video presets (Fast, Balanced, Quality, Lossless)
2. Verify bitrate changes
3. Measure quality differences

**Expected Results:**
- Bitrate increases with quality
- Visual quality improves
- Performance trade-off expected
- All presets functional

### 8.2 Audio Quality Settings

**Test Procedure:**
1. Test all audio quality levels
2. Measure output bitrate
3. Analyze audio quality

**Expected Results:**
- Bitrate matches setting
- Quality increases with setting
- All levels functional
- No artifacts

### 8.3 Hardware Encoding Toggle

**Test Procedure:**
1. Record with hardware ON
2. Record with hardware OFF
3. Compare outputs

**Expected Results:**
- Both modes work
- Performance difference notable
- Quality comparable
- Toggle effective

---

## Test Results Template

### Test Execution Report

**Date:** YYYY-MM-DD  
**Tester:** [Name]  
**Build:** [Version/Commit]  
**Test Environment:** [OS, Hardware]

#### Test Summary
| Category | Tests Run | Passed | Failed | Skipped |
|----------|-----------|--------|--------|---------|
| Unit | | | | |
| Integration | | | | |
| Performance | | | | |
| Quality | | | | |
| Stress | | | | |
| **Total** | | | | |

#### Failed Tests
| Test ID | Test Name | Failure Reason | Severity | Status |
|---------|-----------|----------------|----------|--------|
| | | | | |

#### Performance Metrics
| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| CPU Usage (HW) | <5% overhead | | |
| CPU Usage (SW) | <20% overhead | | |
| Memory Usage | <500MB | | |
| Frame Drops | <1% | | |
| Video Latency | <5ms | | |
| Audio Latency | <2ms | | |

#### Quality Metrics
| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| PSNR | >35 dB | | |
| SSIM | >0.95 | | |
| Audio THD+N | <0.01% | | |
| Sync Drift | <50ms | | |

#### Notes and Observations
- 
- 
- 

#### Recommendations
- 
- 
- 

---

## Validation Checklist

Before declaring Phase 4 Task 5 complete, verify:

### Functional
- [ ] EncoderPipeline initializes successfully
- [ ] Texture conversion works correctly
- [ ] AAC encoding produces valid output
- [ ] Multi-track recording functional
- [ ] Configuration API works
- [ ] Legacy pipeline unaffected
- [ ] Feature flag controls pipeline selection

### Performance
- [ ] Hardware encoding provides benefit
- [ ] Software fallback works
- [ ] CPU usage within targets
- [ ] Memory usage stable
- [ ] No performance regression vs Phase 3
- [ ] Frame drops minimal
- [ ] Encoding latency acceptable

### Quality
- [ ] Visual quality matches or exceeds legacy
- [ ] Audio quality matches or exceeds legacy
- [ ] No artifacts or glitches
- [ ] Professional tools import successfully
- [ ] Multi-track playback works
- [ ] Codec compliance verified

### Stability
- [ ] No crashes in normal operation
- [ ] No memory leaks
- [ ] Error handling robust
- [ ] Clean shutdown always
- [ ] Long recordings stable
- [ ] Stress tests pass

### Compatibility
- [ ] Intel hardware encoding works
- [ ] AMD hardware encoding works
- [ ] NVIDIA hardware encoding works
- [ ] Software encoding works
- [ ] Windows 10 compatible
- [ ] Windows 11 compatible

---

## Acceptance Criteria

Phase 4 Task 5 is complete when:

1. **All critical tests pass** (unit, integration, basic performance)
2. **Performance targets met** (CPU <5% overhead with HW)
3. **Quality validated** (PSNR >35 dB, professional tools import)
4. **Stability confirmed** (no crashes, leaks, or data loss)
5. **Documentation complete** (this guide + task 5.7)

**Sign-off required from:**
- Developer: Implementation complete
- Tester: All tests passed
- Reviewer: Code review approved

---

## Next Steps

After Sub-Task 5.6 completion:
1. Address any failing tests
2. Document findings in test report
3. Proceed to Sub-Task 5.7 (Documentation)
4. Update architecture diagrams
5. Create migration guide
6. Prepare for Task 6 (C# Layer Integration)

---

**Document Version:** 1.0  
**Status:** Active Testing Phase  
**Next Review:** After test completion
