using CaptureTool.Application.Abstractions.Analysis.Media;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows.Media;

public sealed class WindowsVideoAnalysisFrameSource : IVideoAnalysisFrameSource
{
    public const long MaximumFrameCount = 50_000;
    public const int MaximumSampledFrameCount = 1_000;

    public IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
        Stream video,
        CancellationToken cancellationToken = default)
    {
        return ReadFramesAsync(video, startFrameIndex: 0, cancellationToken);
    }

    public async IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
        Stream video,
        long startFrameIndex,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (!video.CanRead)
        {
            throw new ArgumentException("A video analysis source must be readable.", nameof(video));
        }

        if (startFrameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startFrameIndex));
        }

        string sourcePath = WindowsVideoAnalysisWorkingFiles.CreatePath(".mp4");
        try
        {
            await WindowsVideoAnalysisWorkingFiles.CopyToNewFileAsync(
                video,
                sourcePath,
                cancellationToken).ConfigureAwait(false);

            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
            MediaClip sourceClip = await MediaClip.CreateFromFileAsync(sourceFile);
            VideoEncodingProperties properties = sourceClip.GetVideoEncodingProperties();
            int width = checked((int)properties.Width);
            int height = checked((int)properties.Height);
            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException("The video does not expose a positive frame size.");
            }

            TimeSpan duration = sourceClip.OriginalDuration;
            if (duration <= TimeSpan.Zero)
            {
                yield break;
            }

            long frameDurationTicks = CalculateFrameDurationTicks(properties);
            long frameCount = checked((long)Math.Ceiling(
                duration.Ticks / (double)frameDurationTicks));
            if (frameCount > MaximumFrameCount)
            {
                throw new NotSupportedException(
                    $"Frame-by-frame OCR is limited to {MaximumFrameCount} frames per video.");
            }

            var composition = new MediaComposition();
            composition.Clips.Add(sourceClip);
            for (long frameIndex = startFrameIndex; frameIndex < frameCount; frameIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan startTime = TimeSpan.FromTicks(checked(frameIndex * frameDurationTicks));
                TimeSpan endTime = TimeSpan.FromTicks(Math.Min(
                    duration.Ticks,
                    checked(startTime.Ticks + frameDurationTicks)));
                if (endTime <= startTime)
                {
                    break;
                }

                using IRandomAccessStream thumbnail = await composition.GetThumbnailAsync(
                    startTime,
                    width,
                    height,
                    VideoFramePrecision.NearestFrame);
                var image = new MemoryStream();
                try
                {
                    using Stream thumbnailStream = thumbnail.AsStreamForRead();
                    await thumbnailStream.CopyToAsync(image, cancellationToken)
                        .ConfigureAwait(false);
                    image.Position = 0;
                    yield return new VideoAnalysisFrame(
                        frameIndex,
                        startTime,
                        endTime,
                        image);
                    image = null!;
                }
                finally
                {
                    if (image != null)
                    {
                        await image.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(sourcePath);
        }
    }

    public async IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAtIntervalAsync(
        Stream video,
        TimeSpan interval,
        TimeSpan startTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (!video.CanRead)
        {
            throw new ArgumentException("A video analysis source must be readable.", nameof(video));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (startTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startTime));
        }

        string sourcePath = WindowsVideoAnalysisWorkingFiles.CreatePath(".mp4");
        try
        {
            await WindowsVideoAnalysisWorkingFiles.CopyToNewFileAsync(
                video,
                sourcePath,
                cancellationToken).ConfigureAwait(false);

            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
            MediaClip sourceClip = await MediaClip.CreateFromFileAsync(sourceFile);
            VideoEncodingProperties properties = sourceClip.GetVideoEncodingProperties();
            int width = checked((int)properties.Width);
            int height = checked((int)properties.Height);
            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException("The video does not expose a positive frame size.");
            }

            TimeSpan duration = sourceClip.OriginalDuration;
            if (duration <= TimeSpan.Zero || startTime >= duration)
            {
                yield break;
            }

            long frameDurationTicks = CalculateFrameDurationTicks(properties);
            long sampleCount = checked((long)Math.Ceiling(
                (duration - startTime).Ticks / (double)interval.Ticks));
            if (sampleCount > MaximumSampledFrameCount)
            {
                throw new NotSupportedException(
                    $"Selected-frame analysis is limited to {MaximumSampledFrameCount} frames per video.");
            }

            var composition = new MediaComposition();
            composition.Clips.Add(sourceClip);
            long sampleIndex = checked(startTime.Ticks / interval.Ticks);
            for (TimeSpan sampleTime = startTime;
                 sampleTime < duration;
                 sampleTime += interval, sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan endTime = TimeSpan.FromTicks(Math.Min(
                    duration.Ticks,
                    checked(sampleTime.Ticks + frameDurationTicks)));
                if (endTime <= sampleTime)
                {
                    break;
                }

                using IRandomAccessStream thumbnail = await composition.GetThumbnailAsync(
                    sampleTime,
                    width,
                    height,
                    VideoFramePrecision.NearestFrame);
                var image = new MemoryStream();
                try
                {
                    using Stream thumbnailStream = thumbnail.AsStreamForRead();
                    await thumbnailStream.CopyToAsync(image, cancellationToken)
                        .ConfigureAwait(false);
                    image.Position = 0;
                    yield return new VideoAnalysisFrame(
                        sampleIndex,
                        sampleTime,
                        endTime,
                        image);
                    image = null!;
                }
                finally
                {
                    if (image != null)
                    {
                        await image.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(sourcePath);
        }
    }

    public async IAsyncEnumerable<VideoAnalysisFrame> ReadSampledFramesAsync(
        Stream video,
        TimeSpan preferredInterval,
        int maximumSampleCount,
        TimeSpan startTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (!video.CanRead)
        {
            throw new ArgumentException("A video analysis source must be readable.", nameof(video));
        }

        if (preferredInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredInterval));
        }

        if (maximumSampleCount < 2 || maximumSampleCount > MaximumSampledFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSampleCount));
        }

        if (startTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startTime));
        }

        string sourcePath = WindowsVideoAnalysisWorkingFiles.CreatePath(".mp4");
        try
        {
            await WindowsVideoAnalysisWorkingFiles.CopyToNewFileAsync(
                video,
                sourcePath,
                cancellationToken).ConfigureAwait(false);

            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
            MediaClip sourceClip = await MediaClip.CreateFromFileAsync(sourceFile);
            VideoEncodingProperties properties = sourceClip.GetVideoEncodingProperties();
            int width = checked((int)properties.Width);
            int height = checked((int)properties.Height);
            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException("The video does not expose a positive frame size.");
            }

            TimeSpan duration = sourceClip.OriginalDuration;
            if (duration <= TimeSpan.Zero || startTime >= duration)
            {
                yield break;
            }

            long frameDurationTicks = CalculateFrameDurationTicks(properties);
            IReadOnlyList<VideoFrameSamplePoint> schedule = CreateSampleSchedule(
                duration,
                TimeSpan.FromTicks(frameDurationTicks),
                preferredInterval,
                maximumSampleCount);

            var composition = new MediaComposition();
            composition.Clips.Add(sourceClip);
            foreach (VideoFrameSamplePoint sample in schedule)
            {
                if (sample.StartTime < startTime)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                using IRandomAccessStream thumbnail = await composition.GetThumbnailAsync(
                    sample.StartTime,
                    width,
                    height,
                    VideoFramePrecision.NearestFrame);
                var image = new MemoryStream();
                try
                {
                    using Stream thumbnailStream = thumbnail.AsStreamForRead();
                    await thumbnailStream.CopyToAsync(image, cancellationToken)
                        .ConfigureAwait(false);
                    image.Position = 0;
                    yield return new VideoAnalysisFrame(
                        sample.SampleIndex,
                        sample.StartTime,
                        sample.EndTime,
                        image,
                        sample.ResumeTime);
                    image = null!;
                }
                finally
                {
                    if (image != null)
                    {
                        await image.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(sourcePath);
        }
    }

    internal static IReadOnlyList<VideoFrameSamplePoint> CreateSampleSchedule(
        TimeSpan duration,
        TimeSpan frameDuration,
        TimeSpan preferredInterval,
        int maximumSampleCount)
    {
        if (duration <= TimeSpan.Zero)
        {
            return [];
        }

        if (frameDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frameDuration));
        }

        if (preferredInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredInterval));
        }

        if (maximumSampleCount < 2 || maximumSampleCount > MaximumSampledFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSampleCount));
        }

        long finalSampleTicks = Math.Max(0, duration.Ticks - frameDuration.Ticks);
        if (finalSampleTicks == 0)
        {
            return [new VideoFrameSamplePoint(0, TimeSpan.Zero, duration, duration)];
        }

        long intervalDivisor = maximumSampleCount - 1L;
        long minimumIntervalTicks = finalSampleTicks / intervalDivisor;
        if (finalSampleTicks % intervalDivisor != 0)
        {
            minimumIntervalTicks++;
        }
        long effectiveIntervalTicks = Math.Max(preferredInterval.Ticks, minimumIntervalTicks);
        var sampleTimes = new List<long>(maximumSampleCount);
        for (long sampleTicks = 0; sampleTicks < finalSampleTicks;)
        {
            sampleTimes.Add(sampleTicks);
            sampleTicks = checked(sampleTicks + effectiveIntervalTicks);
        }

        if (sampleTimes.Count == 0 || sampleTimes[^1] != finalSampleTicks)
        {
            sampleTimes.Add(finalSampleTicks);
        }

        if (sampleTimes.Count > maximumSampleCount)
        {
            throw new InvalidOperationException("The adaptive video sample schedule exceeded its bound.");
        }

        var schedule = new VideoFrameSamplePoint[sampleTimes.Count];
        for (int index = 0; index < sampleTimes.Count; index++)
        {
            TimeSpan sampleTime = TimeSpan.FromTicks(sampleTimes[index]);
            TimeSpan resumeTime = index + 1 < sampleTimes.Count
                ? TimeSpan.FromTicks(sampleTimes[index + 1])
                : duration;
            schedule[index] = new VideoFrameSamplePoint(
                index,
                sampleTime,
                resumeTime,
                resumeTime);
        }

        return schedule;
    }

    internal static long CalculateFrameDurationTicks(VideoEncodingProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        uint numerator = properties.FrameRate.Numerator;
        uint denominator = properties.FrameRate.Denominator;
        if (numerator == 0)
        {
            throw new NotSupportedException("The video does not expose a nominal frame rate.");
        }

        double ticks = TimeSpan.TicksPerSecond * Math.Max(1u, denominator) / (double)numerator;
        if (!double.IsFinite(ticks) || ticks > long.MaxValue)
        {
            throw new NotSupportedException("The video frame rate is not supported.");
        }

        return Math.Max(1, checked((long)Math.Round(ticks)));
    }
}

internal sealed record VideoFrameSamplePoint(
    long SampleIndex,
    TimeSpan StartTime,
    TimeSpan EndTime,
    TimeSpan ResumeTime);
