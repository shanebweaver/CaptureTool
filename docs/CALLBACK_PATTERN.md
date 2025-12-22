# Video Frame and Audio Sample Callback Pattern

This document describes the pattern for exposing video frames and audio samples at the managed/native boundary in CaptureTool.

## Architecture Overview

The callback pattern uses function pointers to forward video frames and audio samples from the native capture layer to the managed C# layer in real-time.

```
┌─────────────────────────────────────────────────────────┐
│                    Managed Layer (C#)                   │
│  ┌───────────────────────────────────────────────────┐ │
│  │   WindowsScreenRecorder                           │ │
│  │   - SetVideoFrameCallback(callback)               │ │
│  │   - SetAudioSampleCallback(callback)              │ │
│  └───────────────────────────────────────────────────┘ │
└──────────────────────┬──────────────────────────────────┘
                       │ P/Invoke
                       ▼
┌─────────────────────────────────────────────────────────┐
│                 Native Boundary (DLL)                   │
│  ┌───────────────────────────────────────────────────┐ │
│  │   ScreenRecorder.h / ScreenRecorder.cpp           │ │
│  │   - SetVideoFrameCallback(callback)               │ │
│  │   - SetAudioSampleCallback(callback)              │ │
│  │   - VideoFrameData struct                         │ │
│  │   - AudioSampleData struct                        │ │
│  └───────────────────────────────────────────────────┘ │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                Native Implementation                    │
│  ┌───────────────────────────────────────────────────┐ │
│  │   WindowsGraphicsCaptureSession                   │ │
│  │   - Forwards frames from FrameArrivedHandler      │ │
│  │   - Forwards samples from AudioCaptureHandler     │ │
│  └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

## Usage Example (C#)

```csharp
using CaptureTool.Domains.Capture.Implementations.Windows;

public class ExampleUsage
{
    private readonly WindowsScreenRecorder _recorder = new();
    
    public void StartRecordingWithCallbacks()
    {
        // Register callbacks before starting recording
        _recorder.SetVideoFrameCallback(OnVideoFrameReceived);
        _recorder.SetAudioSampleCallback(OnAudioSampleReceived);
        
        // Start recording
        var hMonitor = GetPrimaryMonitorHandle();
        _recorder.StartRecording(hMonitor, "output.mp4", captureAudio: true);
    }
    
    private void OnVideoFrameReceived(ref VideoFrameData frameData)
    {
        // Called on native thread - be thread-safe!
        // frameData.pTexture is a pointer to ID3D11Texture2D
        // frameData.Timestamp is in 100ns ticks
        // frameData.Width and frameData.Height are dimensions
        
        Console.WriteLine($"Video frame: {frameData.Width}x{frameData.Height} at {frameData.Timestamp}");
        
        // You can:
        // - Copy the texture for processing
        // - Forward to another pipeline
        // - Analyze frame content
        // - Update UI (marshal to UI thread first!)
    }
    
    private void OnAudioSampleReceived(ref AudioSampleData sampleData)
    {
        // Called on native thread - be thread-safe!
        // sampleData.pData is a pointer to audio bytes
        // sampleData.NumFrames is number of audio frames
        // sampleData.SampleRate, Channels, BitsPerSample describe format
        
        Console.WriteLine($"Audio sample: {sampleData.NumFrames} frames at {sampleData.SampleRate}Hz");
        
        // You can:
        // - Process audio (e.g., apply effects)
        // - Forward to another pipeline
        // - Analyze audio content
        // - Visualize audio waveform
    }
    
    public void StopRecording()
    {
        _recorder.StopRecording();
        
        // Unregister callbacks
        _recorder.SetVideoFrameCallback(null);
        _recorder.SetAudioSampleCallback(null);
    }
}
```

## Data Structures

### VideoFrameData
```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct VideoFrameData
{
    public IntPtr pTexture;     // Pointer to ID3D11Texture2D
    public long Timestamp;      // Timestamp in 100ns ticks
    public uint Width;          // Frame width in pixels
    public uint Height;         // Frame height in pixels
}
```

### AudioSampleData
```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct AudioSampleData
{
    public IntPtr pData;        // Pointer to audio sample data
    public uint NumFrames;      // Number of audio frames
    public long Timestamp;      // Timestamp in 100ns ticks
    public uint SampleRate;     // Sample rate in Hz
    public ushort Channels;     // Number of channels
    public ushort BitsPerSample;// Bits per sample
}
```

## Thread Safety Considerations

⚠️ **Important**: Callbacks are invoked on native threads, not the UI thread!

- **Do NOT** access UI controls directly from callbacks
- **Do NOT** perform long-running operations in callbacks
- **Do** marshal data to appropriate threads
- **Do** minimize processing in callbacks to avoid blocking capture

Example of marshaling to UI thread:
```csharp
private void OnVideoFrameReceived(ref VideoFrameData frameData)
{
    var frameInfo = new { frameData.Width, frameData.Height, frameData.Timestamp };
    
    // Marshal to UI thread
    Dispatcher.InvokeAsync(() =>
    {
        UpdateFrameCountLabel(frameInfo.Width, frameInfo.Height);
    });
}
```

## Performance Considerations

1. **Callbacks are frequent**: 30-60 video frames/second, many audio samples/second
2. **Keep callbacks fast**: Avoid blocking operations
3. **Copy data if needed**: Native memory may be reused after callback returns
4. **Unregister callbacks**: Set to null when done to avoid memory leaks

## Implementation Details

### Native Layer
- `ScreenRecorder.h` defines callback types and data structures
- `ScreenRecorderImpl` stores callbacks and passes them via `CaptureSessionConfig`
- `WindowsGraphicsCaptureSession` invokes callbacks after writing to MP4 sink
- Callbacks use `__stdcall` calling convention for P/Invoke compatibility

### Managed Layer
- `CaptureInterop.cs` declares P/Invoke signatures with proper marshaling
- `VideoFrameCallback` and `AudioSampleCallback` delegates use `UnmanagedFunctionPointer`
- Structs use `StructLayout(LayoutKind.Sequential)` for memory layout compatibility
- `WindowsScreenRecorder` provides type-safe wrapper methods

## Extending the Pattern

To add new callback types:

1. Define data structure in `ScreenRecorder.h`
2. Add callback type and registration function
3. Update `ScreenRecorderImpl` to store and pass callback
4. Update `WindowsGraphicsCaptureSession` to invoke callback
5. Add corresponding C# structs and delegates in `CaptureInterop.cs`
6. Expose in `WindowsScreenRecorder`

## Testing

See `CallbackTests.cpp` for unit tests of the callback mechanism.
