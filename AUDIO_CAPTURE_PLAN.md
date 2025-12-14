# Audio Capture Implementation Plan

This document outlines the implementation plan for adding local desktop audio capture support to the CaptureTool screen recording functionality.

## Overview

The audio capture feature uses Windows Audio Session API (WASAPI) to capture system audio in loopback mode, synchronizes it with video frames, and multiplexes both streams into an MP4 container using Media Foundation.

## Architecture

### Components

1. **AudioCaptureManager** (C++)
   - Manages WASAPI audio capture in loopback mode
   - Runs audio capture on a dedicated thread
   - Provides callbacks for captured audio samples with timestamps
   - Handles audio format negotiation with the system

2. **MP4SinkWriter** (C++ - Enhanced)
   - Extended to support both video and audio streams
   - Handles H.264 video encoding and AAC audio encoding
   - Thread-safe writing using mutex protection
   - Maintains separate timestamps for audio and video

3. **ScreenRecorder** (C++ - Enhanced)
   - Coordinates video capture and audio capture
   - Accepts enableAudio parameter to toggle audio capture
   - Manages lifecycle of AudioCaptureManager

4. **C# Interop Layer**
   - Updated P/Invoke signatures to accept audio flag
   - Passes IsDesktopAudioEnabled state through the call chain

## Technical Details

### Audio Format
- **Input**: PCM audio from WASAPI (system default format, typically 48kHz or 44.1kHz, stereo)
- **Output**: AAC encoded audio in MP4 container
- **Bitrate**: 192 kbps

### Synchronization
- Both audio and video use **QPC (QueryPerformanceCounter)** timestamps
- Timestamps are converted to 100-nanosecond units (Media Foundation standard)
- First timestamp of each stream is used as the zero reference
- Audio and video samples are written with their relative timestamps

### Threading Model
- **Main Thread**: Handles video capture and frame processing
- **Audio Thread**: Dedicated thread for WASAPI audio capture
- **Thread Safety**: Mutex protection in MP4SinkWriter for concurrent audio/video writes

### Error Handling
- Audio capture failures are non-fatal - video recording continues without audio
- Audio initialization failure falls back to video-only mode
- Audio format negotiation uses system default mix format

## Implementation Flow

### Initialization (TryStartRecording)
1. Initialize video capture components (existing code)
2. If `enableAudio` is true:
   - Create AudioCaptureManager instance
   - Initialize WASAPI audio client in loopback mode
   - Get system audio format
3. Initialize MP4SinkWriter with both video and optional audio streams
4. Start audio capture thread
5. Start video capture session

### Capture Loop
- **Video**: Graphics capture API delivers frames to FrameArrivedHandler → WriteFrame()
- **Audio**: WASAPI audio thread captures buffers → callback → WriteAudioSample()
- Both write operations are thread-safe via mutex

### Cleanup (TryStopRecording)
1. Stop audio capture thread
2. Stop video capture session
3. Finalize MP4SinkWriter (flushes both streams)
4. Release all resources

## Files Modified

### C++ Files
- `AudioCaptureManager.h` - NEW: Audio capture manager header
- `AudioCaptureManager.cpp` - NEW: Audio capture implementation
- `MP4SinkWriter.h` - MODIFIED: Added audio support
- `MP4SinkWriter.cpp` - MODIFIED: Audio stream initialization and writing
- `ScreenRecorder.h` - MODIFIED: Added enableAudio parameter
- `ScreenRecorder.cpp` - MODIFIED: Audio manager integration
- `pch.h` - MODIFIED: Added WASAPI and STL headers
- `CaptureInterop.vcxproj` - MODIFIED: Added new files and libraries

### C# Files
- `CaptureInterop.cs` - MODIFIED: Added enableAudio parameter to P/Invoke
- `WindowsScreenRecorder.cs` - MODIFIED: Pass enableAudio to native code
- `IScreenRecorder.cs` - MODIFIED: Interface signature update
- `CaptureToolVideoCaptureHandler.cs` - MODIFIED: Pass IsDesktopAudioEnabled flag

## Testing Checklist

- [ ] Build solution for x64 and ARM64 platforms
- [ ] Test video capture without audio (enableAudio = false)
- [ ] Test video capture with audio (enableAudio = true)
- [ ] Verify audio/video synchronization in output MP4
- [ ] Test with different display resolutions
- [ ] Test with no system audio playing (silent audio)
- [ ] Test starting/stopping multiple recordings
- [ ] Verify no memory leaks or resource cleanup issues
- [ ] Test feature flag integration (VideoCapture_LocalAudio)
- [ ] Verify UI toggle button works correctly

## Known Considerations

1. **System Audio Only**: This implementation captures system audio (loopback), not microphone input
2. **Format Flexibility**: Uses system's default audio format, ensuring compatibility
3. **Graceful Degradation**: Audio failures don't prevent video recording
4. **Synchronization Accuracy**: QPC-based timestamps provide microsecond precision
5. **Resource Usage**: Audio thread has minimal overhead with efficient WASAPI buffering

## Future Enhancements

- Support for microphone input in addition to system audio
- Audio level monitoring/visualization
- Audio device selection (not just default)
- Audio format selection (sample rate, bit depth)
- Audio processing (noise reduction, normalization)
