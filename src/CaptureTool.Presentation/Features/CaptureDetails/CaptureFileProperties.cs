using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis.Payloads;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed record CaptureProperty(string Label, string Value);

/// <summary>Current file properties, independent of optional saved analysis.</summary>
public sealed class CaptureFileProperties
{
    public ObservableCollection<CaptureProperty> Basic { get; } = [];
    public ObservableCollection<CaptureProperty> Technical { get; } = [];
    public string Compact { get; private set; } = string.Empty;
    public bool HasTechnical => Technical.Count > 0;

    public static CaptureFileProperties Create(FileDetailsMetadata file, ILocalizationService text)
    {
        var result = new CaptureFileProperties();
        string Label(string key) => text.GetString("CaptureDetails_" + key);
        void Add(string key, string? value, bool technical = false)
        {
            if (!string.IsNullOrEmpty(value)) (technical ? result.Technical : result.Basic).Add(new(Label(key), value));
        }
        string Date(DateTimeOffset date) => date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var compact = new List<string>();
        Add("MediaType", Label("Media_" + file.MediaKind));
        Add("Size", CaptureDetailsContent.FormatSize(file.SizeBytes));
        if ((file.Image?.Dimensions ?? file.Video?.Dimensions) is { } dimensions)
        {
            string size = $"{dimensions.Width} × {dimensions.Height}";
            compact.Add(size);
            Add("Dimensions", size);
            uint a = dimensions.Width, b = dimensions.Height;
            while (b != 0) (a, b) = (b, a % b);
            Add("AspectRatio", $"{dimensions.Width / a}:{dimensions.Height / a}");
        }
        if (file.Duration is { } duration)
        {
            string time = CaptureDetailsContent.Time(duration);
            compact.Add(time);
            Add("Duration", time);
        }
        compact.Add(CaptureDetailsContent.FormatSize(file.SizeBytes));
        result.Compact = string.Join(" · ", compact);
        Add("CapturedAt", file.CapturedAt is { } captured ? Date(captured) : Label("Unknown"));
        Add("CreatedAt", Date(file.FileCreatedAt));
        Add("ModifiedAt", Date(file.FileModifiedAt));
        Add("ContentType", file.ContentType, true);
        Add("DpiX", file.Image?.DpiX?.ToString("0.##", CultureInfo.CurrentCulture), true);
        Add("DpiY", file.Image?.DpiY?.ToString("0.##", CultureInfo.CurrentCulture), true);
        Add("FrameRate", file.Video?.FrameRate?.ToString("0.##", CultureInfo.CurrentCulture), true);
        Add("VideoBitrate", file.Video?.Bitrate is { } vb ? vb.ToString("N0", CultureInfo.CurrentCulture) + " bps" : null, true);
        Add("VideoCodec", file.Video?.Codec, true);
        Add("Channels", file.Audio?.Channels?.ToString(CultureInfo.CurrentCulture), true);
        Add("SampleRate", file.Audio?.SampleRate is { } hz ? hz.ToString("N0", CultureInfo.CurrentCulture) + " Hz" : null, true);
        Add("AudioBitrate", file.Audio?.Bitrate is { } ab ? ab.ToString("N0", CultureInfo.CurrentCulture) + " bps" : null, true);
        Add("AudioCodec", file.Audio?.Codec, true);
        return result;
    }
}
