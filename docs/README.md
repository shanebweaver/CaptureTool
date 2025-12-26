# CaptureTool Documentation

## Active Documentation

### [SESSION_ARCHITECTURE.md](SESSION_ARCHITECTURE.md)
Capture session architecture and design patterns.
- Core components and responsibilities
- Architectural patterns (DI, RAII, separation of concerns)
- Lifecycle and data flow
- Shutdown sequence and thread safety

### [CALLBACK_PATTERN.md](CALLBACK_PATTERN.md)
API reference for real-time video/audio callback pattern.
- Usage examples and data structures
- Thread safety considerations
- Performance characteristics

### [NEXT_IMPROVEMENTS.md](NEXT_IMPROVEMENTS.md) 🆕
**Start here for new development!** Focused roadmap of remaining improvements.
- Test doubles for unit testing
- Clock pause/resume implementation
- Dependency injection audit
- Architecture polish

## Archive

Historical documentation from previous PRs has been moved to `docs/archive/`:
- Implementation summaries (PRs #157, #162, #163)
- Detailed recommendations (superseded by NEXT_IMPROVEMENTS.md)
- Callback implementation details

See [archive/README.md](archive/README.md) for details.

---

## Quick Start

### For Understanding Architecture
1. Read [SESSION_ARCHITECTURE.md](SESSION_ARCHITECTURE.md) for architectural overview
2. Review [CALLBACK_PATTERN.md](CALLBACK_PATTERN.md) for API reference

### For New Development
1. Read [NEXT_IMPROVEMENTS.md](NEXT_IMPROVEMENTS.md) for the roadmap
   - See what's already been completed
   - Understand remaining improvements and priorities
   - Follow the implementation roadmap

## Key Concepts

### Function Pointer Callbacks
```csharp
// Register callbacks before recording
recorder.SetVideoFrameCallback(OnVideoFrame);
recorder.SetAudioSampleCallback(OnAudioSample);

// Start recording (frames/samples forwarded to callbacks)
recorder.StartRecording(monitor, "output.mp4", true);
```

### Data Structures
```csharp
// Video frame metadata
struct VideoFrameData {
    IntPtr pTexture;    // ID3D11Texture2D pointer
    long Timestamp;     // 100ns ticks
    uint Width;         // Pixels
    uint Height;        // Pixels
}

// Audio sample metadata
struct AudioSampleData {
    IntPtr pData;       // Audio bytes pointer
    uint NumFrames;     // Frame count
    long Timestamp;     // 100ns ticks
    uint SampleRate;    // Hz
    ushort Channels;    // Channel count
    ushort BitsPerSample; // Bit depth
}
```

### Thread Safety
⚠️ **Critical**: Callbacks run on native threads!
```csharp
void OnVideoFrame(ref VideoFrameData frame) {
    // Called on native background thread
    
    // Marshal to UI thread if needed
    Dispatcher.InvokeAsync(() => UpdateUI(frame));
}
```

## Architecture

```
User App (C#)
    ↓
WindowsScreenRecorder
    ↓
CaptureInterop (P/Invoke)
    ↓
[Native Boundary]
    ↓
ScreenRecorder.dll
    ↓
WindowsGraphicsCaptureSession
    ↓
FrameArrivedHandler / AudioCaptureHandler
    ↓
Callbacks invoked on native threads
```

## Implementation Status

✅ Native layer implementation complete  
✅ Managed layer implementation complete  
✅ Unit tests added  
✅ Comprehensive documentation  
✅ Security review completed  
✅ Example code provided  
✅ Ready for Windows CI/CD validation

## Testing

Unit tests in `src/CaptureInterop.Tests/CallbackTests.cpp`:
- Callback registration tests
- Data structure layout validation
- Setter method verification

Integration testing requires Windows build environment.

## Contributing

When extending the callback pattern:
1. Update data structures in `ScreenRecorder.h`
2. Add P/Invoke declarations in `CaptureInterop.cs`
3. Implement callback invocation in native layer
4. Add tests in `CallbackTests.cpp`
5. Update documentation
6. Consider security implications

## Questions?

For questions or issues:
1. Check [CALLBACK_PATTERN.md](CALLBACK_PATTERN.md) for API reference
2. Check [SESSION_ARCHITECTURE.md](SESSION_ARCHITECTURE.md) for architecture
3. Check [NEXT_IMPROVEMENTS.md](NEXT_IMPROVEMENTS.md) for development roadmap
4. Check [archive/](archive/) for historical context and implementation details
