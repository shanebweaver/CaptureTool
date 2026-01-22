using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Interfaces;

/// <summary>
/// Represents a video file that is still being finalized after recording stops.
/// Provides async notification when the file is ready for playback.
/// </summary>
public sealed class PendingVideoFile : VideoFile
{
    private readonly TaskCompletionSource _finalizationTask = new();

    public string FileName => Path.GetFileName(FilePath);
    public static FilePickerType FilePickerType => FilePickerType.Video;

    public PendingVideoFile(string path) : base(path)
    {
    }

    /// <summary>
    /// Gets a task that completes when the video file is fully finalized and ready for playback.
    /// </summary>
    public Task WhenReadyAsync() => _finalizationTask.Task;

    /// <summary>
    /// Signals that the video finalization is complete.
    /// </summary>
    public void Complete()
    {
        _finalizationTask.TrySetResult();
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
