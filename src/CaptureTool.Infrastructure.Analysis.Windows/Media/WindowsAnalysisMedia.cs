using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Analysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows.Media;

/// <summary>Bounded shared decoding for all providers. Frames/chunks are valid only until the next iteration.</summary>
internal sealed class WindowsAnalysisMedia(IScratchArtifactStore scratch)
{
    internal const int MaximumImageDimension = 2048;
    internal static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(2);
    internal static readonly TimeSpan AudioChunkDuration = TimeSpan.FromSeconds(15);

    public static async Task<SoftwareBitmap> LoadImageAsync(string path, CancellationToken ct)
        => await DecodeSourceAsync(async () =>
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
        using IRandomAccessStream stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false);
        return await DecodeAsync(stream, ct).ConfigureAwait(false);
    }).ConfigureAwait(false);

    private static async Task<SoftwareBitmap> DecodeAsync(IRandomAccessStream stream, CancellationToken ct)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0) throw new InvalidDataException("Invalid image dimensions.");
        double scale = Math.Min(1, MaximumImageDimension / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)) },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<(SoftwareBitmap Bitmap, TimeSpan Timestamp)> ReadFramesAsync(string path, int maximumFrames,
        TimeSpan preferredInterval, [EnumeratorCancellation] CancellationToken ct)
    {
        StorageFile file = await DecodeSourceAsync(() => StorageFile.GetFileFromPathAsync(path).AsTask(ct)).ConfigureAwait(false);
        MediaClip clip = await DecodeSourceAsync(() => MediaClip.CreateFromFileAsync(file).AsTask(ct)).ConfigureAwait(false);
        ValidateDuration(clip.OriginalDuration);
        VideoEncodingProperties format = clip.GetVideoEncodingProperties();
        if (format.Width == 0 || format.Height == 0) throw new InvalidAnalysisMediaException();
        double scale = Math.Min(1, MaximumImageDimension / (double)Math.Max(format.Width, format.Height));
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        int count = Math.Min(maximumFrames, Math.Max(1, (int)Math.Ceiling(clip.OriginalDuration.TotalSeconds / preferredInterval.TotalSeconds)));
        for (int index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            // Container duration can extend beyond the final decodable frame; sample inside each interval.
            TimeSpan timestamp = TimeSpan.FromTicks(clip.OriginalDuration.Ticks * index / count);
            using ImageStream thumbnail = await DecodeSourceAsync(() => composition.GetThumbnailAsync(timestamp,
                Math.Max(1, (int)(format.Width * scale)), Math.Max(1, (int)(format.Height * scale)), VideoFramePrecision.NearestFrame).AsTask(ct)).ConfigureAwait(false);
            using SoftwareBitmap bitmap = await DecodeSourceAsync(() => DecodeAsync(thumbnail, ct)).ConfigureAwait(false);
            yield return (bitmap, timestamp);
        }
    }

    public async IAsyncEnumerable<AudioChunk> ReadAudioChunksAsync(string path, AnalysisMediaKind mediaKind,
        [EnumeratorCancellation] CancellationToken ct)
    {
        StorageFile source = await DecodeSourceAsync(() => StorageFile.GetFileFromPathAsync(path).AsTask(ct)).ConfigureAwait(false);
        TimeSpan duration;
        if (mediaKind == AnalysisMediaKind.Video)
        {
            MediaClip clip = await DecodeSourceAsync(() => MediaClip.CreateFromFileAsync(source).AsTask(ct)).ConfigureAwait(false);
            if (clip.EmbeddedAudioTracks.Count == 0) yield break;
            duration = clip.OriginalDuration;
        }
        else
        {
            BackgroundAudioTrack track = await DecodeSourceAsync(() => BackgroundAudioTrack.CreateFromFileAsync(source).AsTask(ct)).ConfigureAwait(false);
            duration = track.OriginalDuration;
        }
        ValidateDuration(duration);
        // Keep one leased path for the iterator's lifetime, including time spent in a provider.
        // Clearing temporary files must not remove either the current chunk or its directory.
        string outputPath = scratch.CreateLeasedArtifactPath("analysis-audio", ".wav", maximumRetainedArtifacts: 8);
        try
        {
            for (TimeSpan offset = TimeSpan.Zero; offset < duration; offset += AudioChunkDuration)
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan end = offset + AudioChunkDuration < duration ? offset + AudioChunkDuration : duration;
                try
                {
                    using (FileStream placeholder = new(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                    StorageFile output = await StorageFile.GetFileFromPathAsync(outputPath).AsTask(ct).ConfigureAwait(false);
                    var transcoder = new MediaTranscoder { TrimStartTime = offset, TrimStopTime = end };
                    MediaEncodingProfile profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);
                    profile.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16);
                    PrepareTranscodeResult prepared = await transcoder.PrepareFileTranscodeAsync(source, output, profile).AsTask(ct).ConfigureAwait(false);
                    if (!prepared.CanTranscode) throw new InvalidAnalysisMediaException();
                    await prepared.TranscodeAsync().AsTask(ct).ConfigureAwait(false);
                    if (new FileInfo(outputPath).Length > 1024 * 1024) throw new InvalidDataException("Decoded audio exceeds its chunk bound.");
                    yield return new(outputPath, offset, end - offset);
                }
                finally { File.Delete(outputPath); }
            }
        }
        finally { scratch.DeleteArtifact(outputPath); }
    }

    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > MaximumDuration) throw new InvalidAnalysisMediaException();
    }

    private static async Task<T> DecodeSourceAsync<T>(Func<Task<T>> decode)
    {
        try { return await decode().ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException)
        { throw new InvalidAnalysisMediaException(); }
    }

}

internal sealed record AudioChunk(string Path, TimeSpan Offset, TimeSpan Duration);
internal sealed class InvalidAnalysisMediaException : Exception;
