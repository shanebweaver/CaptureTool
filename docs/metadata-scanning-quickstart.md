# Metadata Scanning System - Quick Start

## Overview

The metadata scanning system allows you to register custom scanners that analyze video frames and audio samples to extract timestamped information. The system processes scans asynchronously with progress tracking.

## Key Components

### Interfaces
- **IVideoMetadataScanner**: Implement to scan video frames
- **IAudioMetadataScanner**: Implement to scan audio samples
- **IMetadataScanningService**: Queue and manage scan jobs
- **IMetadataScannerRegistry**: Register/unregister scanners

### Models
- **MetadataEntry**: Single metadata record with timestamp
- **MetadataFile**: Complete scan results (saved as JSON)
- **IMetadataScanJob**: Track scan progress and status

## Quick Start

### 1. Create a Custom Scanner

```csharp
public class MyVideoScanner : IVideoMetadataScanner
{
    public string ScannerId => "my-scanner";
    public string Name => "My Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Video;

    public async Task<MetadataEntry?> ScanFrameAsync(
        VideoFrameData frameData, 
        CancellationToken cancellationToken = default)
    {
        // Analyze the frame
        var result = AnalyzeFrame(frameData);
        
        return new MetadataEntry(
            frameData.Timestamp,
            ScannerId,
            "my-data",
            result
        );
    }
}
```

### 2. Register the Scanner

```csharp
// In DI setup
services.AddSingleton<IVideoMetadataScanner, MyVideoScanner>();

// After building service provider
serviceProvider.RegisterMetadataScanners();
```

### 3. Queue a Scan Job

```csharp
var scanningService = serviceProvider.GetRequiredService<IMetadataScanningService>();
var job = scanningService.QueueScan("path/to/video.mp4");

// Track progress
job.ProgressChanged += (s, progress) => 
    Console.WriteLine($"Progress: {progress}%");

job.StatusChanged += (s, status) => 
{
    if (status == MetadataScanJobStatus.Completed)
        Console.WriteLine($"Done! Output: {job.MetadataFilePath}");
};
```

## Built-in Scanners

The system includes two example scanners:
- **BasicVideoFrameScanner**: Extracts frame dimensions
- **BasicAudioSampleScanner**: Extracts audio format info

## Output Format

Metadata is saved as JSON:

```json
{
  "sourceFilePath": "video.mp4",
  "scanTimestamp": "2025-12-28T06:00:00Z",
  "scannerInfo": {
    "my-scanner": "My Scanner"
  },
  "entries": [
    {
      "timestamp": 12345678,
      "scannerId": "my-scanner",
      "key": "my-data",
      "value": "result",
      "additionalData": {...}
    }
  ]
}
```

## Features

✅ Async processing (doesn't block UI)  
✅ Progress tracking  
✅ Cancellation support  
✅ Queue management  
✅ Extensible scanner system  
✅ Thread-safe  
✅ Event-driven updates  

## Documentation

- **Architecture Details**: [docs/architecture/metadata-scanning.md](architecture/metadata-scanning.md)
- **Usage Examples**: [docs/examples/MetadataScanningExample.cs](examples/MetadataScanningExample.cs)

## Example Use Cases

- Motion detection in video frames
- Silence detection in audio
- Scene change detection
- Object recognition
- Speech-to-text transcription
- Activity logging
- Performance metrics collection
