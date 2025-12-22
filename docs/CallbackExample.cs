using System;
using System.Runtime.InteropServices;
using CaptureTool.Domains.Capture.Implementations.Windows;

// Note: The callback types are internal to the Windows implementation assembly.
// In your own application, you would need to:
// 1. Reference CaptureTool.Domains.Capture.Implementations.Windows
// 2. Add InternalsVisibleTo attribute if accessing from another assembly
// 3. Use the fully qualified type names as shown below

// Import the callback types from CaptureInterop (internal to the implementation assembly)
using VideoFrameCallback = CaptureTool.Domains.Capture.Implementations.Windows.CaptureInterop.VideoFrameCallback;
using AudioSampleCallback = CaptureTool.Domains.Capture.Implementations.Windows.CaptureInterop.AudioSampleCallback;
using VideoFrameData = CaptureTool.Domains.Capture.Implementations.Windows.VideoFrameData;
using AudioSampleData = CaptureTool.Domains.Capture.Implementations.Windows.AudioSampleData;

namespace CaptureTool.Examples;

/// <summary>
/// Example demonstrating how to use video frame and audio sample callbacks
/// to receive frames and samples as they are captured.
/// </summary>
public class CallbackExample
{
    private readonly WindowsScreenRecorder _recorder = new();
    private int _videoFrameCount = 0;
    private int _audioSampleCount = 0;
    
    // Keep references to prevent garbage collection
    private VideoFrameCallback? _videoCallback;
    private AudioSampleCallback? _audioCallback;

    public void StartCapture()
    {
        Console.WriteLine("Starting capture with callbacks...");
        
        // Create callback delegates (keep references to prevent GC)
        _videoCallback = OnVideoFrameReceived;
        _audioCallback = OnAudioSampleReceived;
        
        // Register callbacks before starting recording
        _recorder.SetVideoFrameCallback(_videoCallback);
        _recorder.SetAudioSampleCallback(_audioCallback);
        
        // Start recording to file
        var primaryMonitor = GetPrimaryMonitorHandle();
        bool started = _recorder.StartRecording(
            hMonitor: primaryMonitor,
            outputPath: "example_output.mp4",
            captureAudio: true
        );
        
        if (started)
        {
            Console.WriteLine("Recording started successfully!");
            Console.WriteLine("Callbacks will be invoked as frames/samples arrive.");
        }
        else
        {
            Console.WriteLine("Failed to start recording.");
        }
    }

    public void StopCapture()
    {
        Console.WriteLine($"Stopping capture. Received {_videoFrameCount} video frames and {_audioSampleCount} audio samples.");
        
        // Stop recording
        _recorder.StopRecording();
        
        // Unregister callbacks
        _recorder.SetVideoFrameCallback(null);
        _recorder.SetAudioSampleCallback(null);
        
        // Clear references
        _videoCallback = null;
        _audioCallback = null;
        
        Console.WriteLine("Capture stopped.");
    }

    /// <summary>
    /// Called on native thread when a video frame is captured.
    /// WARNING: This is NOT the UI thread! Marshal to UI thread if needed.
    /// </summary>
    private void OnVideoFrameReceived(ref VideoFrameData frameData)
    {
        _videoFrameCount++;
        
        // Example: Log frame information
        if (_videoFrameCount % 30 == 0) // Log every 30 frames (~1 second at 30fps)
        {
            var timestamp = TimeSpan.FromTicks(frameData.Timestamp);
            Console.WriteLine(
                $"[Video] Frame #{_videoFrameCount}: " +
                $"{frameData.Width}x{frameData.Height} at {timestamp.TotalSeconds:F2}s"
            );
        }
        
        // Example use cases:
        // 1. Process video frame (e.g., apply filters, detect objects)
        // 2. Forward to another pipeline (e.g., streaming server)
        // 3. Display preview (marshal to UI thread)
        // 4. Analyze frame content (e.g., motion detection)
        
        // Note: frameData.pTexture points to ID3D11Texture2D
        // You would need to use Direct3D interop to access pixel data
    }

    /// <summary>
    /// Called on native thread when an audio sample is captured.
    /// WARNING: This is NOT the UI thread! Marshal to UI thread if needed.
    /// </summary>
    private void OnAudioSampleReceived(ref AudioSampleData sampleData)
    {
        _audioSampleCount++;
        
        // Example: Log audio information
        if (_audioSampleCount % 100 == 0) // Log every 100 samples
        {
            var timestamp = TimeSpan.FromTicks(sampleData.Timestamp);
            Console.WriteLine(
                $"[Audio] Sample #{_audioSampleCount}: " +
                $"{sampleData.NumFrames} frames, " +
                $"{sampleData.SampleRate}Hz, " +
                $"{sampleData.Channels}ch, " +
                $"{sampleData.BitsPerSample}bit at {timestamp.TotalSeconds:F2}s"
            );
        }
        
        // Example use cases:
        // 1. Process audio (e.g., apply effects, normalize volume)
        // 2. Forward to another pipeline (e.g., streaming server)
        // 3. Visualize audio (e.g., waveform, spectrum analyzer)
        // 4. Analyze audio content (e.g., voice activity detection)
        
        // Example: Access raw audio data
        if (sampleData.pData != IntPtr.Zero && sampleData.NumFrames > 0)
        {
            // Calculate buffer size
            int bytesPerFrame = (sampleData.BitsPerSample / 8) * sampleData.Channels;
            int bufferSize = (int)sampleData.NumFrames * bytesPerFrame;
            
            // Copy to managed array if you need to process it
            // byte[] audioBuffer = new byte[bufferSize];
            // Marshal.Copy(sampleData.pData, audioBuffer, 0, bufferSize);
            
            // Process audioBuffer as needed...
        }
    }

    /// <summary>
    /// Helper method to get the primary monitor handle.
    /// </summary>
    private static IntPtr GetPrimaryMonitorHandle()
    {
        // In a real application, use Windows API to enumerate monitors
        // For simplicity, this example uses MonitorFromPoint
        return MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
    }

    // P/Invoke declarations for monitor enumeration
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}

/// <summary>
/// Example entry point showing how to use the callback example.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var example = new CallbackExample();
        
        // Start capturing with callbacks
        example.StartCapture();
        
        // Let it run for 10 seconds
        Console.WriteLine("Recording for 10 seconds...");
        Console.WriteLine("Press any key to stop early.");
        
        // Wait for 10 seconds or until key press
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < 10)
        {
            if (Console.KeyAvailable)
            {
                Console.ReadKey(true);
                break;
            }
            System.Threading.Thread.Sleep(100);
        }
        
        // Stop capturing
        example.StopCapture();
        
        Console.WriteLine("Example completed. Press any key to exit.");
        Console.ReadKey();
    }
}
