using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

/// <summary>
/// Represents a video file that is still being finalized after recording stops.
/// Provides async notification when the file is ready for playback.
/// </summary>
public sealed class PendingVideoFile : IVideoFile
{
    private readonly TaskCompletionSource<VideoFile> _finalizationTask = new();

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public FilePickerType FilePickerType => FilePickerType.Video;

    public PendingVideoFile(string path)
    {
        FilePath = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Gets a task that completes when the video file is fully finalized and ready for playback.
    /// </summary>
    public Task<VideoFile> WhenReadyAsync() => _finalizationTask.Task;

    /// <summary>
    /// Signals that the video finalization is complete.
    /// </summary>
    public void Complete(VideoFile videoFile)
    {
        _finalizationTask.TrySetResult(videoFile);
    }

    /// <summary>
    /// Signals that the video finalization failed.
    /// </summary>
    public void Fail(Exception exception)
    {
        _finalizationTask.TrySetException(exception);
    }

    /// <summary>
    /// Checks if the finalization is complete.
    /// </summary>
    public bool IsReady => _finalizationTask.Task.IsCompleted;
}
