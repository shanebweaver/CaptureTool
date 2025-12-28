using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;

namespace CaptureTool.Examples;

/// <summary>
/// Example demonstrating how to integrate metadata scanning with video capture.
/// </summary>
public class MetadataScanningExample
{
    private readonly IVideoCaptureHandler _captureHandler;
    private readonly IMetadataScanningService _scanningService;

    public MetadataScanningExample(
        IVideoCaptureHandler captureHandler,
        IMetadataScanningService scanningService)
    {
        _captureHandler = captureHandler;
        _scanningService = scanningService;
    }

    /// <summary>
    /// Example: Start recording and automatically queue metadata scanning when done.
    /// </summary>
    public void RecordWithMetadataScanning(NewCaptureArgs captureArgs)
    {
        // Start video capture
        _captureHandler.StartVideoCapture(captureArgs);

        // Set up to scan metadata when capture completes
        _captureHandler.NewVideoCaptured += OnVideoCaptured;
    }

    private void OnVideoCaptured(object? sender, IVideoFile videoFile)
    {
        // Unsubscribe to avoid duplicate processing
        _captureHandler.NewVideoCaptured -= OnVideoCaptured;

        // Queue metadata scanning for the captured video
        var scanJob = _scanningService.QueueScan(videoFile.Path);

        // Track progress
        scanJob.ProgressChanged += (s, progress) =>
        {
            Console.WriteLine($"Scanning progress: {progress:F1}%");
        };

        scanJob.StatusChanged += (s, status) =>
        {
            Console.WriteLine($"Scan status: {status}");
            
            if (status == MetadataScanJobStatus.Completed)
            {
                Console.WriteLine($"Metadata file created: {scanJob.MetadataFilePath}");
            }
            else if (status == MetadataScanJobStatus.Failed)
            {
                Console.WriteLine($"Scan failed: {scanJob.ErrorMessage}");
            }
        };
    }

    /// <summary>
    /// Example: Create a custom video metadata scanner.
    /// </summary>
    public class MotionDetectionScanner : IVideoMetadataScanner
    {
        public string ScannerId => "motion-detection";
        public string Name => "Motion Detection Scanner";
        public MetadataScannerType ScannerType => MetadataScannerType.Video;

        public async Task<MetadataEntry?> ScanFrameAsync(
            VideoFrameData frameData, 
            CancellationToken cancellationToken = default)
        {
            // Example: Detect motion in the frame
            // In a real implementation, you would:
            // 1. Access the texture data from frameData.pTexture
            // 2. Compare with previous frame
            // 3. Calculate motion metrics
            
            // Simulate motion detection
            await Task.Delay(1, cancellationToken); // Simulate processing
            bool motionDetected = DetectMotion(frameData);

            if (motionDetected)
            {
                return new MetadataEntry(
                    timestamp: frameData.Timestamp,
                    scannerId: ScannerId,
                    key: "motion-detected",
                    value: true,
                    additionalData: new Dictionary<string, object?>
                    {
                        ["frameWidth"] = frameData.Width,
                        ["frameHeight"] = frameData.Height,
                        ["confidence"] = 0.85
                    }
                );
            }

            return null; // No motion detected
        }

        private bool DetectMotion(VideoFrameData frameData)
        {
            // Placeholder for actual motion detection logic
            return false;
        }
    }

    /// <summary>
    /// Example: Create a custom audio metadata scanner.
    /// </summary>
    public class SilenceDetectionScanner : IAudioMetadataScanner
    {
        public string ScannerId => "silence-detection";
        public string Name => "Silence Detection Scanner";
        public MetadataScannerType ScannerType => MetadataScannerType.Audio;

        private const double SilenceThreshold = 0.01; // Amplitude threshold

        public async Task<MetadataEntry?> ScanSampleAsync(
            AudioSampleData sampleData, 
            CancellationToken cancellationToken = default)
        {
            // Example: Detect silence in audio sample
            // In a real implementation, you would:
            // 1. Access audio data from sampleData.pData
            // 2. Calculate audio levels
            // 3. Detect silence periods
            
            await Task.Delay(1, cancellationToken); // Simulate processing
            bool isSilent = DetectSilence(sampleData);

            if (isSilent)
            {
                return new MetadataEntry(
                    timestamp: sampleData.Timestamp,
                    scannerId: ScannerId,
                    key: "silence-detected",
                    value: true,
                    additionalData: new Dictionary<string, object?>
                    {
                        ["sampleRate"] = sampleData.SampleRate,
                        ["channels"] = sampleData.Channels,
                        ["numFrames"] = sampleData.NumFrames,
                        ["threshold"] = SilenceThreshold
                    }
                );
            }

            return null; // Not silent
        }

        private bool DetectSilence(AudioSampleData sampleData)
        {
            // Placeholder for actual silence detection logic
            return false;
        }
    }

    /// <summary>
    /// Example: Register custom scanners with dependency injection.
    /// </summary>
    public static class CustomScannerRegistration
    {
        public static void RegisterCustomScanners(IServiceProvider serviceProvider)
        {
            var registry = serviceProvider.GetRequiredService<IMetadataScannerRegistry>();

            // Create and register custom scanners
            var motionScanner = new MotionDetectionScanner();
            var silenceScanner = new SilenceDetectionScanner();

            registry.RegisterVideoScanner(motionScanner);
            registry.RegisterAudioScanner(silenceScanner);
        }
    }

    /// <summary>
    /// Example: Monitor all active scan jobs.
    /// </summary>
    public void MonitorScanJobs()
    {
        var activeJobs = _scanningService.GetActiveJobs();

        foreach (var job in activeJobs)
        {
            Console.WriteLine($"Job {job.JobId}:");
            Console.WriteLine($"  File: {job.FilePath}");
            Console.WriteLine($"  Status: {job.Status}");
            Console.WriteLine($"  Progress: {job.Progress:F1}%");
        }
    }

    /// <summary>
    /// Example: Cancel all scan jobs (e.g., when shutting down).
    /// </summary>
    public void CancelAllScans()
    {
        _scanningService.CancelAllJobs();
        Console.WriteLine("All scan jobs cancelled");
    }

    /// <summary>
    /// Example: Get a specific job and wait for completion.
    /// </summary>
    public async Task<string?> WaitForScanCompletion(Guid jobId, TimeSpan timeout)
    {
        var job = _scanningService.GetJob(jobId);
        if (job == null)
        {
            return null;
        }

        var tcs = new TaskCompletionSource<string?>();
        
        job.StatusChanged += (sender, status) =>
        {
            if (status == MetadataScanJobStatus.Completed)
            {
                tcs.TrySetResult(job.MetadataFilePath);
            }
            else if (status == MetadataScanJobStatus.Failed || 
                     status == MetadataScanJobStatus.Cancelled)
            {
                tcs.TrySetResult(null);
            }
        };

        using var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() => tcs.TrySetCanceled());

        try
        {
            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Timeout waiting for scan completion");
            return null;
        }
    }
}
