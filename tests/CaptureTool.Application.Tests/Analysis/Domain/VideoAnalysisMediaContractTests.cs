using CaptureTool.Application.Abstractions.Analysis.Media;
using System.Runtime.CompilerServices;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class VideoAnalysisMediaContractTests
{
    [TestMethod]
    public async Task VideoAnalysisFrame_ShouldExposeTimingAndOwnImageStream()
    {
        var image = new MemoryStream([1, 2, 3], writable: false);
        var frame = new VideoAnalysisFrame(
            7,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2.5),
            image);

        Assert.AreEqual(7, frame.FrameIndex);
        Assert.AreEqual(TimeSpan.FromSeconds(2), frame.StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(2.5), frame.EndTime);
        Assert.AreEqual(TimeSpan.FromSeconds(2.5), frame.ResumeTime);
        Assert.AreSame(image, frame.Image);

        await frame.DisposeAsync();

        Assert.IsFalse(image.CanRead);
    }

    [TestMethod]
    public void VideoAnalysisFrame_ShouldRejectInvalidIndexTimingAndStream()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VideoAnalysisFrame(
            -1,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            new MemoryStream()));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoAnalysisFrame(
            0,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            new MemoryStream()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new VideoAnalysisFrame(
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            null!));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoAnalysisFrame(
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            new MemoryStream(),
            TimeSpan.FromMilliseconds(500)));

        var closed = new MemoryStream();
        closed.Dispose();
        Assert.ThrowsExactly<ArgumentException>(() => new VideoAnalysisFrame(
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            closed));
    }

    [TestMethod]
    public async Task FrameSourceDefaultIntervalReader_ShouldSelectCadenceAndDisposeSkippedFrames()
    {
        var source = new SequentialFrameSource(
            (0, 0d, 1d),
            (1, 1d, 2d),
            (2, 4d, 5d),
            (3, 5d, 6d),
            (4, 8d, 9d));
        var selected = new List<VideoAnalysisFrame>();

        IVideoAnalysisFrameSource frameSource = source;
        await foreach (VideoAnalysisFrame frame in frameSource.ReadFramesAtIntervalAsync(
            new MemoryStream([1], writable: false),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(4)))
        {
            selected.Add(frame);
        }

        CollectionAssert.AreEqual(new long[] { 2, 4 },
            selected.Select(frame => frame.FrameIndex).ToArray());
        CollectionAssert.AreEqual(new long[] { 0, 1, 3 }, source.DisposedFrameIndexes.ToArray());
        foreach (VideoAnalysisFrame frame in selected)
        {
            await frame.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task FrameSourceDefaultIntervalReader_ShouldRejectInvalidIntervalAndStartTime()
    {
        var source = new SequentialFrameSource();
        IVideoAnalysisFrameSource frameSource = source;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            frameSource.ReadFramesAtIntervalAsync(
                new MemoryStream(),
                TimeSpan.Zero,
                TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            frameSource.ReadFramesAtIntervalAsync(
                new MemoryStream(),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(-1)));

        await Task.CompletedTask;
    }

    [TestMethod]
    public async Task FrameSourceDefaultSampledReader_ShouldEnforceBoundAndDisposeOverflowFrame()
    {
        var source = new SequentialFrameSource(
            (0, 0d, 1d),
            (1, 1d, 2d),
            (2, 2d, 3d));
        IVideoAnalysisFrameSource frameSource = source;

        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () =>
        {
            await foreach (VideoAnalysisFrame frame in frameSource.ReadSampledFramesAsync(
                new MemoryStream([1], writable: false),
                TimeSpan.FromSeconds(1),
                maximumSampleCount: 2,
                TimeSpan.Zero))
            {
                await frame.DisposeAsync();
            }
        });

        CollectionAssert.AreEqual(new long[] { 0, 1, 2 }, source.DisposedFrameIndexes.ToArray());
    }

    [TestMethod]
    public void FrameSourceDefaultSampledReader_ShouldRejectInvalidPolicyAndStartTime()
    {
        IVideoAnalysisFrameSource frameSource = new SequentialFrameSource();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            frameSource.ReadSampledFramesAsync(
                new MemoryStream(),
                TimeSpan.Zero,
                maximumSampleCount: 2,
                TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            frameSource.ReadSampledFramesAsync(
                new MemoryStream(),
                TimeSpan.FromSeconds(1),
                maximumSampleCount: 1,
                TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            frameSource.ReadSampledFramesAsync(
                new MemoryStream(),
                TimeSpan.FromSeconds(1),
                maximumSampleCount: 2,
                TimeSpan.FromTicks(-1)));
    }

    [TestMethod]
    public async Task VideoAudioExtractionResult_ShouldOwnSuccessfulAudioAndBoundOtherOutcomes()
    {
        var audio = new MemoryStream([4, 5, 6], writable: false);
        VideoAudioExtractionResult succeeded = VideoAudioExtractionResult.Succeeded(audio);

        Assert.AreEqual(VideoAudioExtractionStatus.Succeeded, succeeded.Status);
        Assert.AreSame(audio, succeeded.Audio);
        await succeeded.DisposeAsync();
        Assert.IsFalse(audio.CanRead);

        VideoAudioExtractionResult[] outcomes =
        [
            VideoAudioExtractionResult.NoAudio,
            VideoAudioExtractionResult.Unsupported,
            VideoAudioExtractionResult.Cancelled,
            VideoAudioExtractionResult.Failed,
        ];
        CollectionAssert.AreEqual(
            new[]
            {
                VideoAudioExtractionStatus.NoAudio,
                VideoAudioExtractionStatus.Unsupported,
                VideoAudioExtractionStatus.Cancelled,
                VideoAudioExtractionStatus.Failed,
            },
            outcomes.Select(outcome => outcome.Status).ToArray());
        Assert.IsTrue(outcomes.All(outcome => outcome.Audio == null));
        foreach (VideoAudioExtractionResult outcome in outcomes)
        {
            await outcome.DisposeAsync();
        }
    }

    [TestMethod]
    public void VideoAudioExtractionResult_ShouldRejectNullAndUnreadableStreams()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            VideoAudioExtractionResult.Succeeded(null!));
        var closed = new MemoryStream();
        closed.Dispose();
        Assert.ThrowsExactly<ArgumentException>(() =>
            VideoAudioExtractionResult.Succeeded(closed));
    }

    private sealed class SequentialFrameSource(
        params (long Index, double Start, double End)[] frames)
        : IVideoAnalysisFrameSource
    {
        public List<long> DisposedFrameIndexes { get; } = [];

        public async IAsyncEnumerable<VideoAnalysisFrame> ReadFramesAsync(
            Stream video,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach ((long index, double start, double end) in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new VideoAnalysisFrame(
                    index,
                    TimeSpan.FromSeconds(start),
                    TimeSpan.FromSeconds(end),
                    new TrackingMemoryStream(index, DisposedFrameIndexes));
            }
        }
    }

    private sealed class TrackingMemoryStream(
        long frameIndex,
        ICollection<long> disposedFrameIndexes) : MemoryStream([1], writable: false)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing && CanRead)
            {
                disposedFrameIndexes.Add(frameIndex);
            }

            base.Dispose(disposing);
        }
    }
}
