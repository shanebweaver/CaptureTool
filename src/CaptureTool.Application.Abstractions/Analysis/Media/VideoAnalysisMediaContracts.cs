namespace CaptureTool.Application.Abstractions.Analysis.Media;

public sealed class VideoAnalysisFrame : IAsyncDisposable
{
    public VideoAnalysisFrame(
        long frameIndex,
        TimeSpan startTime,
        TimeSpan endTime,
        Stream image,
        TimeSpan? resumeTime = null)
    {
        if (frameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        if (startTime < TimeSpan.Zero || endTime <= startTime)
        {
            throw new ArgumentException(
                "A video analysis frame requires a positive, ordered time range.",
                nameof(startTime));
        }

        ArgumentNullException.ThrowIfNull(image);
        if (!image.CanRead)
        {
            throw new ArgumentException("A video analysis frame must be readable.", nameof(image));
        }

        TimeSpan normalizedResumeTime = resumeTime ?? endTime;
        if (normalizedResumeTime < endTime)
        {
            throw new ArgumentException(
                "A video analysis frame resume time cannot precede its evidence range.",
                nameof(resumeTime));
        }

        FrameIndex = frameIndex;
        StartTime = startTime;
        EndTime = endTime;
        ResumeTime = normalizedResumeTime;
        Image = image;
    }

    public long FrameIndex { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan EndTime { get; }

    /// <summary>
    /// Gets the exact media timestamp from which a resumable sampled analysis can continue
    /// without revisiting this frame.
    /// </summary>
    public TimeSpan ResumeTime { get; }

    public Stream Image { get; }

    public ValueTask DisposeAsync()
    {
        return Image.DisposeAsync();
    }
}

public interface IVideoAnalysisFrameSource
{
    IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
        Stream video,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
        Stream video,
        long startFrameIndex,
        CancellationToken cancellationToken = default)
    {
        if (startFrameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startFrameIndex));
        }

        return ReadFromFrameAsync(video, startFrameIndex, cancellationToken);
    }

    IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAtIntervalAsync(
        Stream video,
        TimeSpan interval,
        TimeSpan startTime,
        CancellationToken cancellationToken = default)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (startTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startTime));
        }

        return ReadAtIntervalAsync(video, interval, startTime, cancellationToken);
    }

    IAsyncEnumerable<VideoAnalysisFrame> ReadSampledFramesAsync(
        Stream video,
        TimeSpan preferredInterval,
        int maximumSampleCount,
        TimeSpan startTime,
        CancellationToken cancellationToken = default)
    {
        if (preferredInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredInterval));
        }

        if (maximumSampleCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSampleCount));
        }

        if (startTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startTime));
        }

        return ReadSampledAsync(
            video,
            preferredInterval,
            maximumSampleCount,
            startTime,
            cancellationToken);
    }

    private async IAsyncEnumerable<VideoAnalysisFrame> ReadFromFrameAsync(
        Stream video,
        long startFrameIndex,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (VideoAnalysisFrame frame in ReadFramesAsync(video, cancellationToken)
            .ConfigureAwait(false))
        {
            if (frame.FrameIndex >= startFrameIndex)
            {
                yield return frame;
            }
            else
            {
                await frame.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async IAsyncEnumerable<VideoAnalysisFrame> ReadAtIntervalAsync(
        Stream video,
        TimeSpan interval,
        TimeSpan startTime,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        TimeSpan nextTime = startTime;
        await foreach (VideoAnalysisFrame frame in ReadFramesAsync(video, cancellationToken)
            .ConfigureAwait(false))
        {
            if (frame.StartTime >= nextTime)
            {
                yield return frame;
                nextTime = frame.StartTime + interval;
            }
            else
            {
                await frame.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async IAsyncEnumerable<VideoAnalysisFrame> ReadSampledAsync(
        Stream video,
        TimeSpan preferredInterval,
        int maximumSampleCount,
        TimeSpan startTime,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        int yielded = 0;
        await foreach (VideoAnalysisFrame frame in ReadFramesAtIntervalAsync(
            video,
            preferredInterval,
            startTime,
            cancellationToken).ConfigureAwait(false))
        {
            yielded++;
            if (yielded > maximumSampleCount)
            {
                await frame.DisposeAsync().ConfigureAwait(false);
                throw new NotSupportedException(
                    $"Selected-frame analysis is limited to {maximumSampleCount} frames per video.");
            }

            yield return frame;
        }
    }
}

public enum VideoAudioExtractionStatus
{
    Unknown,
    Succeeded,
    NoAudio,
    Unsupported,
    Cancelled,
    Failed,
}

public sealed class VideoAudioExtractionResult : IAsyncDisposable
{
    private VideoAudioExtractionResult(
        VideoAudioExtractionStatus status,
        Stream? audio)
    {
        Status = status;
        Audio = audio;
    }

    public VideoAudioExtractionStatus Status { get; }

    public Stream? Audio { get; }

    public static VideoAudioExtractionResult Succeeded(Stream audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        if (!audio.CanRead)
        {
            throw new ArgumentException("Extracted video audio must be readable.", nameof(audio));
        }

        return new(VideoAudioExtractionStatus.Succeeded, audio);
    }

    public static VideoAudioExtractionResult NoAudio { get; } = new(
        VideoAudioExtractionStatus.NoAudio,
        null);

    public static VideoAudioExtractionResult Unsupported { get; } = new(
        VideoAudioExtractionStatus.Unsupported,
        null);

    public static VideoAudioExtractionResult Cancelled { get; } = new(
        VideoAudioExtractionStatus.Cancelled,
        null);

    public static VideoAudioExtractionResult Failed { get; } = new(
        VideoAudioExtractionStatus.Failed,
        null);

    public ValueTask DisposeAsync()
    {
        return Audio?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

public interface IVideoAudioExtractionService
{
    Task<VideoAudioExtractionResult> ExtractWaveAudioAsync(
        Stream video,
        CancellationToken cancellationToken = default);
}
