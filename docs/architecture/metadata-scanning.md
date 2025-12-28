# Metadata Scanning Architecture

## Overview

The metadata scanning system provides an extensible framework for analyzing video frames and audio samples during or after screen recording. It allows developers to register custom scanners that extract information and generate timestamped metadata files.

## Architecture Components

### Core Interfaces

#### `IMetadataScanner`
Base interface for all metadata scanners. Defines:
- `ScannerId`: Unique identifier for the scanner
- `Name`: Human-readable scanner name
- `ScannerType`: Type of data processed (Video or Audio)

#### `IVideoMetadataScanner`
Interface for scanners that analyze video frames.
- `ScanFrameAsync(VideoFrameData, CancellationToken)`: Analyzes a frame and returns metadata

#### `IAudioMetadataScanner`
Interface for scanners that analyze audio samples.
- `ScanSampleAsync(AudioSampleData, CancellationToken)`: Analyzes a sample and returns metadata

### Data Models

#### `MetadataEntry`
Represents a single metadata record with:
- `Timestamp`: When the data was captured (100ns ticks)
- `ScannerId`: Which scanner produced this entry
- `Key`: Type/category of metadata
- `Value`: The extracted value
- `AdditionalData`: Optional dictionary for extra context

#### `MetadataFile`
Container for all metadata from a scan job:
- `SourceFilePath`: Path to the scanned file
- `ScanTimestamp`: When the scan was performed
- `Entries`: List of all metadata entries
- `ScannerInfo`: Information about scanners used

### Service Components

#### `IMetadataScannerRegistry`
Manages registration of scanners:
- `RegisterVideoScanner(IVideoMetadataScanner)`: Add a video scanner
- `RegisterAudioScanner(IAudioMetadataScanner)`: Add an audio scanner
- `Unregister(string)`: Remove a scanner by ID
- `GetVideoScanners()`: Get all video scanners
- `GetAudioScanners()`: Get all audio scanners
- `GetAllScanners()`: Get all registered scanners

#### `IMetadataScanningService`
Manages asynchronous scanning jobs:
- `QueueScan(string)`: Queue a file for scanning
- `GetActiveJobs()`: Get all active scan jobs
- `GetJob(Guid)`: Get a specific job by ID
- `CancelAllJobs()`: Cancel all active jobs

#### `IMetadataScanJob`
Tracks progress of a scanning job:
- `JobId`: Unique job identifier
- `FilePath`: File being scanned
- `Status`: Current job status (Queued, Processing, Completed, Failed, Cancelled)
- `Progress`: Progress percentage (0-100)
- `ErrorMessage`: Error details if failed
- `MetadataFilePath`: Path to output file when completed
- Events: `StatusChanged`, `ProgressChanged`
- `Cancel()`: Cancel the job

## Implementation Details

### Persistent Job Queue

The service uses a **persistent file-based queue** to handle scan jobs:

1. **Job Persistence**: When a scan is queued, a JSON file is created in the app's temporary folder (`ScanJobQueue/job-{guid}.json`)
2. **Startup Recovery**: On app startup, all pending jobs from the queue folder are automatically loaded and resumed
3. **Job Processing**: A background task continuously processes jobs from the queue
4. **Completion**: When a job completes successfully:
   - Metadata is saved as `{videofile}.metadata.json` next to the media file
   - The job queue file is deleted
5. **Cancellation**: When a job is cancelled, its queue file is deleted
6. **Failure Handling**: Failed jobs keep their queue files for retry on next startup

### Job Queue File Format

Each job request file contains:
```json
{
  "jobId": "guid",
  "mediaFilePath": "/path/to/video.mp4",
  "createdAt": "2025-12-28T16:00:00Z",
  "scanCriteria": {
    "optional": "scanner-specific-criteria"
  }
}
```

### MetadataScanningService

The service manages the persistent queue:

1. Jobs are saved to disk as JSON files in `{AppTemp}/ScanJobQueue/`
2. On startup, pending jobs are loaded and queued for processing
3. Jobs are processed asynchronously without blocking the UI thread
4. Processing can take as long as needed (no artificial timeouts)
5. Progress is reported via events
6. Jobs can be cancelled via cancellation tokens
7. Completed jobs are removed from active tracking after 5 minutes
8. Job queue files are deleted when jobs complete or are cancelled

### Benefits of Persistent Queue

- **Survives app restarts**: Jobs continue after app is closed and reopened
- **No data loss**: Media files can be scanned even if app crashes during recording
- **Flexible timing**: Scanning can happen later when system resources are available
- **No background service needed**: Simpler app lifecycle management
- **Observable**: Queue folder contents show pending work

### Thread Safety

- `MetadataScannerRegistry` uses locks for thread-safe scanner registration
- `MetadataScanningService` uses concurrent collections for job management
- Each job has its own `CancellationTokenSource` for independent cancellation
- File I/O is properly synchronized

### Output Format

Metadata files are saved as JSON with the structure:
```json
{
  "sourceFilePath": "path/to/video.mp4",
  "scanTimestamp": "2025-12-28T06:00:00Z",
  "scannerInfo": {
    "basic-video-frame": "Basic Video Frame Scanner",
    "basic-audio-sample": "Basic Audio Sample Scanner"
  },
  "entries": [
    {
      "timestamp": 12345678,
      "scannerId": "basic-video-frame",
      "key": "frame-info",
      "value": "1920x1080",
      "additionalData": {
        "width": 1920,
        "height": 1080
      }
    }
  ]
}
```

## Integration Points

### VideoCaptureHandler Integration

The metadata scanning system integrates with the existing capture system:

1. During recording, `VideoFrameCallback` and `AudioSampleCallback` receive data
2. Callbacks can collect frame/sample data for later scanning
3. After recording completes, call `IMetadataScanningService.QueueScan(filePath)`
4. The service processes the file asynchronously without blocking UI

### Dependency Injection

Register services in your DI container:

```csharp
services.AddWindowsCaptureDomains();
// After building the service provider:
serviceProvider.RegisterMetadataScanners();
```

## Creating Custom Scanners

### Video Scanner Example

```csharp
public class CustomVideoScanner : IVideoMetadataScanner
{
    public string ScannerId => "custom-video";
    public string Name => "Custom Video Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Video;

    public async Task<MetadataEntry?> ScanFrameAsync(
        VideoFrameData frameData, 
        CancellationToken cancellationToken = default)
    {
        // Analyze the frame data
        // Extract information
        // Return metadata entry or null
        
        return new MetadataEntry(
            frameData.Timestamp,
            ScannerId,
            "custom-data",
            myExtractedValue,
            additionalData: myExtraData
        );
    }
}
```

### Audio Scanner Example

```csharp
public class CustomAudioScanner : IAudioMetadataScanner
{
    public string ScannerId => "custom-audio";
    public string Name => "Custom Audio Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;

    public async Task<MetadataEntry?> ScanSampleAsync(
        AudioSampleData sampleData, 
        CancellationToken cancellationToken = default)
    {
        // Analyze the audio sample
        // Extract information
        // Return metadata entry or null
        
        return new MetadataEntry(
            sampleData.Timestamp,
            ScannerId,
            "audio-analysis",
            myExtractedValue,
            additionalData: myExtraData
        );
    }
}
```

### Registering Custom Scanners

```csharp
// In DI setup
services.AddSingleton<IVideoMetadataScanner, CustomVideoScanner>();
services.AddSingleton<IAudioMetadataScanner, CustomAudioScanner>();

// After building service provider
serviceProvider.RegisterMetadataScanners();
```

## Usage Examples

### Queuing a Scan Job

```csharp
var scanningService = serviceProvider.GetRequiredService<IMetadataScanningService>();
var job = scanningService.QueueScan("path/to/video.mp4");

// Track progress
job.ProgressChanged += (sender, progress) =>
{
    Console.WriteLine($"Progress: {progress}%");
};

job.StatusChanged += (sender, status) =>
{
    Console.WriteLine($"Status: {status}");
    if (status == MetadataScanJobStatus.Completed)
    {
        Console.WriteLine($"Metadata saved to: {job.MetadataFilePath}");
    }
};
```

### Cancelling a Job

```csharp
var job = scanningService.QueueScan("path/to/video.mp4");
// Later...
job.Cancel();
```

### Getting Active Jobs

```csharp
var activeJobs = scanningService.GetActiveJobs();
foreach (var job in activeJobs)
{
    Console.WriteLine($"{job.JobId}: {job.Progress}% - {job.Status}");
}
```

## Performance Considerations

1. **Async Processing**: Jobs run in background to avoid UI blocking
2. **Cancellation Support**: All long-running operations support cancellation
3. **Progress Tracking**: Real-time progress updates for UI feedback
4. **Queue Management**: Jobs are processed sequentially to manage resource usage
5. **Scanner Independence**: Scanners are independent and can be added/removed dynamically

## Future Enhancements

Potential improvements to the system:

1. **Parallel Processing**: Process multiple jobs simultaneously
2. **Priority Queues**: Allow high-priority scan jobs
3. **Incremental Processing**: Process during capture instead of post-capture
4. **Result Caching**: Cache scanner results to avoid re-processing
5. **Scanner Pipelines**: Chain scanners together for composite analysis
6. **Custom Output Formats**: Support multiple metadata file formats (XML, CSV, etc.)
7. **Real-time Streaming**: Stream metadata as it's generated
8. **Scanner Configuration**: Allow scanners to accept configuration parameters
