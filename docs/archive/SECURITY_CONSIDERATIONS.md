# Security Considerations for Callback Pattern

## Overview
This document outlines security considerations for the callback pattern implementation that exposes video frames and audio samples at the managed/native boundary.

## Potential Security Concerns

### 1. Function Pointer Safety
**Issue**: Managed delegates passed to native code could be garbage collected, causing crashes.

**Mitigation**:
- Documentation clearly states to keep references to delegates alive
- Example code demonstrates proper lifetime management
- Callbacks can be set to null to unregister safely

### 2. Pointer Lifetime
**Issue**: `VideoFrameData.pTexture` and `AudioSampleData.pData` are raw pointers that may become invalid after callback returns.

**Mitigation**:
- Documentation warns that pointers are only valid during callback
- Users must copy data if needed beyond callback scope
- Native layer controls lifetime of underlying objects

### 3. Thread Safety
**Issue**: Callbacks are invoked on native threads, not UI thread.

**Mitigation**:
- Documentation explicitly warns about thread context
- Example code shows proper thread marshaling
- No shared mutable state between threads

### 4. Buffer Overflows
**Issue**: Audio data pointer with incorrect size could cause buffer overflow.

**Mitigation**:
- `AudioSampleData` includes `numFrames`, `channels`, and `bitsPerSample` for size validation
- Users must calculate buffer size: `numFrames * channels * (bitsPerSample / 8)`
- Native layer validates buffer sizes before writing

### 5. Null Pointer Dereference
**Issue**: Callback pointers or data pointers could be null.

**Mitigation**:
- Native code checks if callback is set before invoking
- Native code checks if format is available before invoking audio callback
- Data structures use `const` for immutable data
- Example code checks for null pointers before access

### 6. Resource Leaks
**Issue**: Callbacks could hold references preventing cleanup.

**Mitigation**:
- Documentation emphasizes clearing callbacks when done
- `StopRecording()` can be called while callbacks are active
- No circular references between native and managed layers

## Safe Usage Pattern

```csharp
public class SafeCallbackUsage
{
    private VideoFrameCallback? _videoCallback;
    private AudioSampleCallback? _audioCallback;
    
    public void Start()
    {
        // Keep references to prevent GC
        _videoCallback = OnVideoFrame;
        _audioCallback = OnAudioSample;
        
        _recorder.SetVideoFrameCallback(_videoCallback);
        _recorder.SetAudioSampleCallback(_audioCallback);
        _recorder.StartRecording(...);
    }
    
    public void Stop()
    {
        _recorder.StopRecording();
        
        // Clear callbacks to release resources
        _recorder.SetVideoFrameCallback(null);
        _recorder.SetAudioSampleCallback(null);
        
        // Clear references
        _videoCallback = null;
        _audioCallback = null;
    }
    
    private void OnVideoFrame(ref VideoFrameData frameData)
    {
        // Check for valid data
        if (frameData.pTexture == IntPtr.Zero)
            return;
            
        // Don't hold references to native pointers
        // Copy data if needed beyond this scope
        
        // Marshal to UI thread if needed
        Dispatcher.InvokeAsync(() => UpdateUI(...));
    }
    
    private void OnAudioSample(ref AudioSampleData sampleData)
    {
        // Validate data
        if (sampleData.pData == IntPtr.Zero || sampleData.numFrames == 0)
            return;
            
        // Calculate buffer size safely
        int bytesPerFrame = (sampleData.bitsPerSample / 8) * sampleData.channels;
        int bufferSize = (int)sampleData.numFrames * bytesPerFrame;
        
        // Copy if needed (don't access after callback returns)
        byte[] buffer = new byte[bufferSize];
        Marshal.Copy(sampleData.pData, buffer, 0, bufferSize);
        
        // Process copied data...
    }
}
```

## Security Review Checklist

✅ **Function Pointers**: Lifetime managed via references
✅ **Pointer Validation**: Null checks before access
✅ **Buffer Sizes**: Validated using metadata fields
✅ **Thread Safety**: Documented and examples provided
✅ **Resource Cleanup**: Clear cleanup path documented
✅ **Memory Safety**: No shared mutable state
✅ **Error Handling**: Failures don't crash native layer

## Recommendations for Users

1. **Always validate pointers**: Check for null before dereferencing
2. **Copy data if needed**: Don't hold native pointers beyond callback
3. **Keep delegates alive**: Maintain references to prevent GC
4. **Clear callbacks**: Set to null when done to release resources
5. **Marshal threads**: Use Dispatcher for UI updates
6. **Validate sizes**: Calculate buffer sizes using provided metadata
7. **Handle errors**: Wrap callback code in try-catch

## Native Layer Protections

The native implementation includes these protections:

```cpp
// Check callback is set before invoking
if (m_config.videoFrameCallback)
{
    VideoFrameData frameData;
    // ... populate data ...
    m_config.videoFrameCallback(&frameData);
}

// Check format is available for audio
if (m_config.audioSampleCallback && args.pFormat)
{
    AudioSampleData sampleData;
    // ... populate data ...
    m_config.audioSampleCallback(&sampleData);
}
```

## Conclusion

The callback pattern implementation follows secure coding practices:
- Validates all inputs before use
- Documents lifetime and thread safety requirements
- Provides safe example code
- Includes proper error handling
- Avoids shared mutable state
- Provides clear cleanup mechanisms

As long as users follow the documented patterns, the implementation is safe and secure.
