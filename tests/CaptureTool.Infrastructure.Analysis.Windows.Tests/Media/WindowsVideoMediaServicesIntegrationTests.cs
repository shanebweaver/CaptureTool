using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Media;

[TestClass]
public sealed class WindowsVideoMediaServicesIntegrationTests
{
    [TestMethod]
    public void SampleSchedule_ShouldAdaptToBoundWorkAndCoverFirstAndFinalFrames()
    {
        TimeSpan duration = TimeSpan.FromHours(2);
        TimeSpan frameDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        IReadOnlyList<VideoFrameSamplePoint> schedule =
            WindowsVideoAnalysisFrameSource.CreateSampleSchedule(
                duration,
                frameDuration,
                TimeSpan.FromSeconds(1),
                WindowsVideoAnalysisFrameSource.MaximumSampledFrameCount);

        Assert.HasCount(
            WindowsVideoAnalysisFrameSource.MaximumSampledFrameCount,
            schedule);
        Assert.AreEqual(TimeSpan.Zero, schedule[0].StartTime);
        Assert.AreEqual(duration - frameDuration, schedule[^1].StartTime);
        Assert.AreEqual(duration, schedule[^1].EndTime);
        Assert.AreEqual(duration, schedule[^1].ResumeTime);
        Assert.IsTrue(schedule.Zip(schedule.Skip(1)).All(pair =>
            pair.First.EndTime == pair.Second.StartTime &&
            pair.First.ResumeTime == pair.Second.StartTime &&
            pair.First.SampleIndex + 1 == pair.Second.SampleIndex));
    }

    [TestMethod]
    public void SampleSchedule_ForShortVideo_ShouldUsePreferredCadenceAndIncludeFinalFrame()
    {
        TimeSpan duration = TimeSpan.FromSeconds(3.2);
        TimeSpan frameDuration = TimeSpan.FromMilliseconds(200);

        IReadOnlyList<VideoFrameSamplePoint> schedule =
            WindowsVideoAnalysisFrameSource.CreateSampleSchedule(
                duration,
                frameDuration,
                TimeSpan.FromSeconds(1),
                maximumSampleCount: 1000);

        CollectionAssert.AreEqual(
            new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3),
            },
            schedule.Select(sample => sample.StartTime).ToArray());
        Assert.AreEqual(duration, schedule[^1].EndTime);
    }

    [TestMethod]
    public async Task SyntheticVideo_ShouldYieldTimedFramesAndExtractReadableWaveAudio()
    {
        string folderPath = Path.Combine(
            Path.GetTempPath(),
            "CaptureTool",
            "VideoAnalysisTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folderPath);
        try
        {
            string videoPath = await CreateSyntheticVideoAsync(folderPath);

            var frameSource = new WindowsVideoAnalysisFrameSource();
            var observedFrames = new List<(long Index, TimeSpan Start, TimeSpan End)>();
            await using (var video = new FileStream(
                videoPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await foreach (VideoAnalysisFrame frame in frameSource.ReadFramesAsync(video))
                {
                    await using (frame.ConfigureAwait(false))
                    {
                        Assert.IsTrue(frame.Image.CanRead);
                        Assert.IsGreaterThan(0, frame.Image.Length);
                        observedFrames.Add((frame.FrameIndex, frame.StartTime, frame.EndTime));
                    }
                }
            }

            Assert.IsNotEmpty(observedFrames);
            Assert.AreEqual(0, observedFrames[0].Index);
            Assert.AreEqual(TimeSpan.Zero, observedFrames[0].Start);
            Assert.IsTrue(observedFrames.Zip(observedFrames.Skip(1)).All(pair =>
                pair.First.End == pair.Second.Start &&
                pair.First.Index + 1 == pair.Second.Index));

            var observedSamples = new List<(long Index, TimeSpan Start, TimeSpan End, TimeSpan Resume)>();
            await using (var video = new FileStream(
                videoPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await foreach (VideoAnalysisFrame frame in frameSource.ReadSampledFramesAsync(
                    video,
                    TimeSpan.FromMilliseconds(250),
                    maximumSampleCount: 4,
                    TimeSpan.Zero))
                {
                    await using (frame.ConfigureAwait(false))
                    {
                        Assert.IsGreaterThan(0, frame.Image.Length);
                        observedSamples.Add((
                            frame.FrameIndex,
                            frame.StartTime,
                            frame.EndTime,
                            frame.ResumeTime));
                    }
                }
            }

            Assert.IsGreaterThanOrEqualTo(2, observedSamples.Count);
            Assert.IsLessThanOrEqualTo(4, observedSamples.Count);
            Assert.AreEqual(TimeSpan.Zero, observedSamples[0].Start);
            Assert.AreEqual(observedSamples[^1].End, observedSamples[^1].Resume);
            Assert.IsTrue(observedSamples.Zip(observedSamples.Skip(1)).All(pair =>
                pair.First.End == pair.Second.Start &&
                pair.First.Resume == pair.Second.Start &&
                pair.First.Index + 1 == pair.Second.Index));

            var audioExtractor = new WindowsVideoAudioExtractionService();
            await using var source = new FileStream(
                videoPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using VideoAudioExtractionResult extraction = await audioExtractor
                .ExtractWaveAudioAsync(source);

            Assert.AreEqual(VideoAudioExtractionStatus.Succeeded, extraction.Status);
            Assert.IsNotNull(extraction.Audio);
            var header = new byte[4];
            await extraction.Audio.ReadExactlyAsync(header);
            CollectionAssert.AreEqual("RIFF"u8.ToArray(), header);
        }
        finally
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, recursive: true);
            }
        }
    }

    private static async Task<string> CreateSyntheticVideoAsync(string folderPath)
    {
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
        StorageFile image = await folder.CreateFileAsync(
            "frame.png",
            CreationCollisionOption.FailIfExists);
        await WriteImageAsync(image);

        string wavePath = Path.Combine(folderPath, "audio.wav");
        WriteSilentWave(wavePath, TimeSpan.FromSeconds(1));
        StorageFile wave = await StorageFile.GetFileFromPathAsync(wavePath);

        MediaClip clip = await MediaClip.CreateFromImageFileAsync(
            image,
            TimeSpan.FromSeconds(1));
        BackgroundAudioTrack audio = await BackgroundAudioTrack.CreateFromFileAsync(wave);
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        composition.BackgroundAudioTracks.Add(audio);

        StorageFile video = await folder.CreateFileAsync(
            "synthetic.mp4",
            CreationCollisionOption.FailIfExists);
        TranscodeFailureReason failure;
        try
        {
            failure = await composition.RenderToFileAsync(
                video,
                MediaTrimmingPreference.Precise,
                MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
        }
        catch (System.Runtime.InteropServices.COMException exception)
        {
            Assert.Fail($"Synthetic video render failed with 0x{exception.HResult:X8}: {exception.Message}");
            throw;
        }
        Assert.AreEqual(TranscodeFailureReason.None, failure);
        return video.Path;
    }

    private static async Task WriteImageAsync(StorageFile image)
    {
        const int Width = 320;
        const int Height = 180;
        byte[] pixels = new byte[Width * Height * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0xCC;
            pixels[index + 1] = 0x66;
            pixels[index + 2] = 0x22;
            pixels[index + 3] = 0xFF;
        }

        using IRandomAccessStream stream = await image.OpenAsync(FileAccessMode.ReadWrite);
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            Width,
            Height,
            96,
            96,
            pixels);
        await encoder.FlushAsync();
    }

    private static void WriteSilentWave(string path, TimeSpan duration)
    {
        const int SampleRate = 16_000;
        const short ChannelCount = 1;
        const short BitsPerSample = 16;
        int sampleCount = checked((int)Math.Ceiling(duration.TotalSeconds * SampleRate));
        int dataLength = checked(sampleCount * ChannelCount * BitsPerSample / 8);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(SampleRate * ChannelCount * BitsPerSample / 8);
        writer.Write((short)(ChannelCount * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }
}
