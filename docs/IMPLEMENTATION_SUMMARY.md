# Summary: Callback Pattern Implementation

## Problem Statement
The goal was to update the boundary between the managed (C#) and native (C++) layer to expose video frames and audio samples as they are received, enabling real-time processing in the managed layer.

## Solution: Function Pointer Callback Pattern

We implemented a callback-based pattern using function pointers that allows native code to invoke managed code callbacks when video frames and audio samples are captured.

### Key Design Decisions

1. **Function Pointers over Events**: Used C-style function pointers with `__stdcall` calling convention for efficient P/Invoke marshaling.

2. **Data Structures**: Created simple POD (Plain Old Data) structures that can be safely marshaled across the boundary:
   - `VideoFrameData`: Contains texture pointer, timestamp, width, height
   - `AudioSampleData`: Contains data pointer, frame count, timestamp, and format info

3. **Dual-Write Architecture**: Frames and samples are BOTH written to the MP4 file AND forwarded to callbacks, maintaining existing functionality while adding new capability.

4. **Configuration-Based Propagation**: Callbacks flow through `CaptureSessionConfig`, ensuring they reach the capture session before recording starts.

5. **Thread Safety**: Callbacks are invoked on native capture threads with proper synchronization. Managed code must marshal to UI thread if needed.

## Architecture Components

### Native Layer (C++)

```
ScreenRecorder.h/cpp (DLL Boundary)
    ↓
ScreenRecorderImpl (Recorder Logic)
    ↓
CaptureSessionConfig (Configuration)
    ↓
WindowsGraphicsCaptureSession (Capture Session)
    ↓
FrameArrivedHandler / AudioCaptureHandler (Capture Sources)
```

### Managed Layer (C#)

```
WindowsScreenRecorder (Public API)
    ↓
CaptureInterop (P/Invoke Declarations)
    ↓
[Native Boundary via P/Invoke]
```

## Usage Flow

1. **Setup**: User creates callback delegates and registers them via `SetVideoFrameCallback()` / `SetAudioSampleCallback()`
2. **Start**: User calls `StartRecording()` which passes callbacks to native layer via config
3. **Capture**: As frames/samples arrive, native code invokes callbacks on capture threads
4. **Processing**: User callback receives data and can process, forward, or analyze
5. **Cleanup**: User calls `StopRecording()` and clears callbacks

## Key Features

✅ **Minimal Changes**: Only adds new functionality without modifying existing code paths
✅ **Backward Compatible**: Existing code works without callbacks
✅ **Efficient**: Direct function pointer calls with minimal marshaling overhead
✅ **Flexible**: User can register/unregister callbacks at any time
✅ **Safe**: Proper lifetime management and thread synchronization
✅ **Well-Documented**: Comprehensive docs, examples, and flow diagrams

## Files Changed

### Native Layer
- `src/CaptureInterop/ScreenRecorder.h` - Added callback types and exports
- `src/CaptureInterop/ScreenRecorder.cpp` - Implemented callback registration
- `src/CaptureInterop.Lib/ScreenRecorderImpl.h` - Added callback storage
- `src/CaptureInterop.Lib/ScreenRecorderImpl.cpp` - Implemented callback setters
- `src/CaptureInterop.Lib/CaptureSessionConfig.h` - Added callback fields
- `src/CaptureInterop.Lib/WindowsGraphicsCaptureSession.cpp` - Implemented callback invocation

### Managed Layer
- `src/CaptureTool.Domains.Capture.Implementations.Windows/CaptureInterop.cs` - P/Invoke and types
- `src/CaptureTool.Domains.Capture.Implementations.Windows/WindowsScreenRecorder.cs` - Public API

### Testing
- `src/CaptureInterop.Tests/CallbackTests.cpp` - Unit tests
- `src/CaptureInterop.Tests/CaptureInterop.Tests.vcxproj` - Added test to project

### Documentation
- `docs/CALLBACK_PATTERN.md` - Usage guide and implementation details
- `docs/CALLBACK_FLOW.md` - Flow and sequence diagrams
- `docs/CallbackExample.cs` - Complete working example

## Thread Safety Considerations

⚠️ **Critical**: Callbacks are invoked on native threads, NOT the UI thread!

- Video callbacks run on `FrameArrivedHandler` background thread
- Audio callbacks run on high-priority WASAPI capture thread
- Managed code MUST marshal to UI thread for UI updates
- Use `Dispatcher.InvokeAsync()` or similar for thread marshaling

## Performance Characteristics

- **Video**: ~30-60 callbacks per second (depends on frame rate)
- **Audio**: ~100-200 callbacks per second (depends on buffer size)
- **Overhead**: Minimal - single function pointer call + struct copy
- **Latency**: Near real-time - callbacks invoked immediately after capture

## Future Enhancements

Potential extensions to this pattern:

1. **Batch Callbacks**: Group multiple frames/samples for efficiency
2. **Frame Filtering**: Allow managed code to request specific frame types
3. **Metadata**: Include additional capture metadata (dropped frames, etc.)
4. **Performance Counters**: Expose capture performance metrics
5. **Error Callbacks**: Notify managed code of capture errors

## Testing

The implementation includes:
- Unit tests for callback registration and data structure layout
- Integration tests (to be run on Windows build agents)
- Example code demonstrating real-world usage

## Conclusion

This implementation provides a clean, efficient, and well-documented pattern for exposing video frames and audio samples to the managed layer. The design maintains backward compatibility while enabling powerful new scenarios for real-time processing, analysis, and forwarding of capture data.
