using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureMemory;

public sealed class CaptureMemoryEvidenceViewModel
{
    public CaptureMemoryEvidenceViewModel(CaptureMemoryMatchEvidence evidence,
        Func<Task> open, ILocalizationService? localization = null)
    {
        Evidence = evidence;
        SourceLabel = SourceName(evidence.MatchKind, localization);
        OpenCommand = new AsyncRelayCommand(open, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        DetailLabel = evidence.IsCombinedMatch
            ? GetString(localization, "Analysis_MatchAcrossPassages", "Across multiple passages")
            : evidence.IsApproximate ? GetString(localization, "Analysis_SimilarText", "Similar text") : string.Empty;
    }

    public CaptureMemoryMatchEvidence Evidence { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public string SourceLabel { get; }
    public string SourceGlyph => Glyph(Evidence.MatchKind);
    public string Text => Evidence.Snippet;
    public IReadOnlyList<CaptureTextRange> Highlights => Evidence.Highlights;
    public bool HasTimecode => Evidence.Timecode.HasValue;
    public string TimecodeLabel => Evidence.Timecode is TimeSpan time ? FormatTimecode(time) : string.Empty;
    public string DetailLabel { get; }
    public bool HasDetailLabel => DetailLabel.Length > 0;
    public string AutomationName => $"{SourceLabel} {TimecodeLabel}. {Text}. {DetailLabel}";

    public static string SourceName(CaptureMemoryMatchKind kind, ILocalizationService? localization = null) => kind switch
    {
        CaptureMemoryMatchKind.OcrText => GetString(localization, "Analysis_Source_ImageText", "Text in image"),
        CaptureMemoryMatchKind.VideoOcrText => GetString(localization, "Analysis_Source_ScreenText", "On-screen text"),
        CaptureMemoryMatchKind.SpeechTranscript => GetString(localization, "Analysis_Source_Transcript", "Transcript"),
        CaptureMemoryMatchKind.ImageDescription or CaptureMemoryMatchKind.VideoDescription =>
            GetString(localization, "Analysis_Source_Description", "AI description"),
        _ => GetString(localization, "Analysis_Source_Filename", "Filename"),
    };

    public static string Glyph(CaptureMemoryMatchKind kind) => kind switch
    {
        CaptureMemoryMatchKind.OcrText or CaptureMemoryMatchKind.VideoOcrText => "\uE8E9",
        CaptureMemoryMatchKind.SpeechTranscript => "\uE720",
        CaptureMemoryMatchKind.ImageDescription or CaptureMemoryMatchKind.VideoDescription => "\uE8B9",
        _ => "\uE8A5",
    };

    public static string FormatTimecode(TimeSpan time) => time.TotalHours >= 1
        ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    internal static string GetString(ILocalizationService? localization, string key, string fallback)
    {
        string? value = localization?.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}
