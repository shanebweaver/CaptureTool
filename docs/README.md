# CaptureTool Documentation

This directory contains documentation for CaptureTool's architecture, design patterns, and implementation details.

## Architecture Documentation

### [SESSION_ARCHITECTURE.md](SESSION_ARCHITECTURE.md)
**Capture session architecture and design patterns.**
- Core components (session, sources, clock, sink)
- Architectural patterns (dependency injection, RAII, separation of concerns)
- Lifecycle and data flow diagrams
- Shutdown sequence and thread safety
- Key design decisions

## Implementation Documentation

### [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)
**Start here!** High-level overview of the design, architecture, and key decisions.
- Problem statement and solution approach
- Architecture components and data flow
- Key features and design decisions
- Files changed and testing strategy

### [CALLBACK_PATTERN.md](CALLBACK_PATTERN.md)
Complete usage guide and API reference.
- Architecture diagram
- C# usage examples
- Data structure specifications
- Thread safety considerations
- Performance characteristics
- Extension points

### [CALLBACK_FLOW.md](CALLBACK_FLOW.md)
Visual flow diagrams and sequence diagrams.
- High-level component diagram
- Detailed sequence diagram
- Thread context visualization
- Key interaction points

### [CallbackExample.cs](CallbackExample.cs)
Complete, working example code.
- Full implementation showing callback usage
- Proper lifetime management
- Thread marshaling examples
- Error handling patterns
- Console application entry point

### [SECURITY_CONSIDERATIONS.md](SECURITY_CONSIDERATIONS.md)
Security analysis and best practices.
- Potential security concerns identified
- Mitigations implemented
- Safe usage patterns
- Security checklist
- Recommendations for users

## Quick Start

1. Read [SESSION_ARCHITECTURE.md](SESSION_ARCHITECTURE.md) for architectural overview
2. Read [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for implementation details
3. Study [CallbackExample.cs](CallbackExample.cs) for usage patterns
4. Review [CALLBACK_PATTERN.md](CALLBACK_PATTERN.md) for detailed API reference
5. Check [SECURITY_CONSIDERATIONS.md](SECURITY_CONSIDERATIONS.md) for safety guidelines

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

For questions or issues with the callback pattern:
1. Check the documentation in this directory
2. Review the example code
3. Examine the flow diagrams
4. Consult the security considerations
