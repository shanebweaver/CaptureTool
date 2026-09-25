using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Local properties shared by the editor and post-capture scanning.</summary>
internal sealed class WindowsMediaFileDetailsReader : IMediaFileDetailsReader
{
    public async Task<FileDetailsMetadata?> ReadAsync(string path, AnalysisMediaKind kind, DateTimeOffset? capturedAt = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(kind)) return null;
        StorageFile file;
        BasicProperties basic;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
            basic = await file.GetBasicPropertiesAsync().AsTask(ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileError(exception) || exception is ArgumentException)
        { return null; }

        ImageFileDetails? image = null;
        VideoFileDetails? video = null;
        AudioFileDetails? audio = null;
        TimeSpan? duration = null;
        string? contentType = Text(file.ContentType);
        try
        {
            switch (kind)
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
        return new FileDetailsMetadata(kind, file.Name, checked((long)basic.Size), contentType,
            file.DateCreated, basic.DateModified, capturedAt, duration, image, video, audio);
    }

    private static AudioFileDetails Audio(AudioEncodingProperties format) =>
        new(Positive(format.ChannelCount), Positive(format.SampleRate), Positive(format.Bitrate), Text(format.Subtype));

    private static uint? Positive(uint value) => value > 0 ? value : null;
    private static double? Positive(double value) => double.IsFinite(value) && value > 0 ? value : null;
    private static string? Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsFileError(Exception exception) => exception is IOException or UnauthorizedAccessException or COMException;
}
