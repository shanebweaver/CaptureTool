# Persistent Job Queue Implementation Summary

## User Request

Replace the in-memory job queue with a persistent file-based system that:
- Keeps track of active jobs across app restarts
- Uses temp folder for job queue files (JSON format)
- Saves metadata next to media files
- Removes 5-second timeout limitation
- Avoids background service complexity

## Implementation (Commits bb22b66 & 8eb0ecd)

### New Components

1. **ScanJobRequest Model** (`ScanJobRequest.cs`)
   - Persistent job data: JobId, MediaFilePath, CreatedAt, ScanCriteria
   - JSON serializable
   - Saved to temp folder: `{AppTemp}/ScanJobQueue/job-{guid}.json`

2. **PersistentJobQueueManager** (`PersistentJobQueueManager.cs`)
   - Manages job queue folder lifecycle
   - `SaveJobRequestAsync()`: Saves job to disk
   - `LoadPendingJobsAsync()`: Loads jobs on startup
   - `DeleteJobRequest()`: Removes completed/cancelled jobs
   - Auto-creates queue folder if missing

### Updated Components

**MetadataScanningService**:
- Added `IStorageService` dependency (for temp folder location)
- Constructor now loads pending jobs on startup (async, non-blocking)
- `QueueScan()` saves job request to disk before processing (async)
- Jobs process without timeout limits (can take as long as needed)
- Completed jobs removed from active tracking after 5 minutes
- Job queue files deleted when complete or cancelled
- Failed jobs kept in queue for retry on next startup
- Changed disposal timeout from 5 to 30 seconds

### Job Lifecycle

```
1. Queue Job
   └─> Create ScanJobRequest
   └─> Save to {AppTemp}/ScanJobQueue/job-{guid}.json
   └─> Add to processing queue

2. Process Job
   └─> Load media file
   └─> Run scanners
   └─> Generate metadata
   └─> Save to {mediafile}.metadata.json
   └─> Delete job-{guid}.json
   └─> Mark complete

3. App Restart
   └─> Load all *.json from ScanJobQueue/
   └─> Verify media files exist
   └─> Queue for processing
```

### File Structure

**Job Queue (temporary)**:
```
{AppTemp}/ScanJobQueue/
├── job-abc123.json
├── job-def456.json
└── ...
```

**Metadata Output (permanent)**:
```
/path/to/video.mp4
/path/to/video.metadata.json  ← Created here
```

### Key Benefits

✅ **Survives App Restart**: Jobs persist across sessions
✅ **No Timeout Limits**: Scans can take as long as needed
✅ **Observable**: Queue folder shows pending work
✅ **Simple Lifecycle**: No background service complications
✅ **Automatic Retry**: Failed jobs retry on next startup
✅ **Clean Output**: Metadata saved next to media files
✅ **Proper Async**: No blocking calls, prevents deadlocks

### Technical Improvements

1. **Async Patterns**: 
   - LoadPendingJobs runs on background thread without blocking constructor
   - SaveJobRequest runs asynchronously without blocking caller
   - Prevents potential deadlocks in UI thread

2. **Error Handling**:
   - Failed file I/O logged but doesn't crash service
   - Missing media files skipped with warning
   - Corrupted job files logged and skipped

3. **Resource Management**:
   - Jobs disposed after 5 minutes of completion
   - Job queue files cleaned up automatically
   - Graceful 30-second shutdown timeout

## Testing

- All 9 existing unit tests pass
- No breaking changes to public API
- Backward compatible with existing code

## Documentation

Updated `docs/architecture/metadata-scanning.md` with:
- Persistent queue architecture details
- Job file format specification
- Startup recovery process
- Benefits of persistent approach
