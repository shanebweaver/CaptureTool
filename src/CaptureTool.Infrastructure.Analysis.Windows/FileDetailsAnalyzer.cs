using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Reads local filesystem/container properties without decoding pixels, sampling, or acquiring models.</summary>
internal sealed class FileDetailsAnalyzer : IMediaAnalyzer
{
    public MediaAnalyzerDescriptor Descriptor { get; } = new("windows-file-details", AnalysisCapability.FileDetails,
        [AnalysisMediaKind.Image, AnalysisMediaKind.Audio, AnalysisMediaKind.Video]);

    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Descriptor.SupportedMedia.Contains(kind) ? AnalyzerAvailability.Ready : AnalyzerAvailability.Unsupported);
    }

    public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(AnalyzerAvailability.Ready);
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Descriptor.SupportedMedia.Contains(input.MediaKind))
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-media");
        StorageFile file;
        BasicProperties basic;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(input.SourcePath).AsTask(ct).ConfigureAwait(false);
            basic = await file.GetBasicPropertiesAsync().AsTask(ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileError(exception) || exception is ArgumentException)
        { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "file-unavailable"); }

        ImageFileDetails? image = null;
        VideoFileDetails? video = null;
        AudioFileDetails? audio = null;
        TimeSpan? duration = null;
        string? contentType = Text(file.ContentType);
        try
        {
            switch (input.MediaKind)
            {
                case AnalysisMediaKind.Image:
                    using (var stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);
                        if (decoder.OrientedPixelWidth > 0 && decoder.OrientedPixelHeight > 0)
                            image = new(new(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight), Positive(decoder.DpiX), Positive(decoder.DpiY));
                        contentType = decoder.DecoderInformation.MimeTypes.FirstOrDefault() ?? contentType;
                    }
                    break;
                case AnalysisMediaKind.Video:
                    MediaClip clip = await MediaClip.CreateFromFileAsync(file).AsTask(ct).ConfigureAwait(false);
                    duration = clip.OriginalDuration > TimeSpan.Zero ? clip.OriginalDuration : null;
                    VideoEncodingProperties format = clip.GetVideoEncodingProperties();
                    VideoProperties properties = await file.Properties.GetVideoPropertiesAsync().AsTask(ct).ConfigureAwait(false);
                    bool rotated = properties.Orientation is VideoOrientation.Rotate90 or VideoOrientation.Rotate270;
                    if (format.Width > 0 && format.Height > 0)
                        video = new(new(rotated ? format.Height : format.Width, rotated ? format.Width : format.Height),
                            format.FrameRate.Denominator > 0 ? Positive(format.FrameRate.Numerator / (double)format.FrameRate.Denominator) : null,
                            Positive(format.Bitrate), Text(format.Subtype));
                    if (clip.EmbeddedAudioTracks.Count > 0) audio = Audio(clip.EmbeddedAudioTracks[0].GetAudioEncodingProperties());
                    break;
                case AnalysisMediaKind.Audio:
                    BackgroundAudioTrack track = await BackgroundAudioTrack.CreateFromFileAsync(file).AsTask(ct).ConfigureAwait(false);
                    duration = track.OriginalDuration > TimeSpan.Zero ? track.OriginalDuration : null;
                    audio = Audio(track.GetAudioEncodingProperties());
                    break;
            }
        }
        catch (Exception exception) when (IsFileError(exception) || exception is ArgumentException or NotSupportedException)
        {
            // An unsupported/corrupt media header must not discard readable filesystem facts.
            // Unavailable details are null, not fabricated zero values or a reason to stop later steps.
        }
        ct.ThrowIfCancellationRequested();
        return AnalyzerOutcome.Success(new FileDetailsMetadata(input.MediaKind, file.Name, checked((long)basic.Size), contentType,
            file.DateCreated, basic.DateModified, input.CapturedAt, duration, image, video, audio),
            new(Descriptor.Id, "windows", "file-properties", "1"));
    }

    private static AudioFileDetails Audio(AudioEncodingProperties format) =>
        new(Positive(format.ChannelCount), Positive(format.SampleRate), Positive(format.Bitrate), Text(format.Subtype));

    private static uint? Positive(uint value) => value > 0 ? value : null;
    private static double? Positive(double value) => double.IsFinite(value) && value > 0 ? value : null;
    private static string? Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsFileError(Exception exception) => exception is IOException or UnauthorizedAccessException or COMException;
}
