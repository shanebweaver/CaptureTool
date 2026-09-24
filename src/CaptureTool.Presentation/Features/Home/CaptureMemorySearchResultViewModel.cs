using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using System.Globalization;
using System.Collections.ObjectModel;
using CaptureTool.Presentation.Features.CaptureMemory;

namespace CaptureTool.Presentation.Features.Home;

public sealed class CaptureMemorySearchResultViewModel : ViewModelBase
{
    public CaptureMemorySearchResultViewModel(
        CaptureMemorySearchResult result,
        CaptureMemoryResultLocation location,
        ILocalizationService? localizationService = null,
        Func<CaptureMemorySearchResultViewModel, CaptureMemoryMatchEvidence, Task>? openEvidence = null,
        string query = "")
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        if (result.CaptureId != location.CaptureId)
        {
            throw new ArgumentException("A Memory result and its resolved location must identify the same capture.", nameof(location));
        }

        CaptureId = result.CaptureId;
        SearchContext = new(query, result.DocumentRevision, result.SourceRevision);
        TotalMatchCount = result.TotalMatchCount;
        EvidencePreviews = new(new ObservableCollection<CaptureMemoryEvidenceViewModel>(result.Matches.Take(2).Select(evidence => new CaptureMemoryEvidenceViewModel(
            evidence, () => openEvidence?.Invoke(this, evidence) ?? Task.CompletedTask, localizationService))));
        ViewAllMatchesLabel = string.Format(CultureInfo.CurrentCulture,
            GetString(localizationService, "CaptureMemory_ViewAllMatches", "View all {0} matches"), TotalMatchCount);
        DurationLabel = result.Duration is TimeSpan duration ? FormatTimecode(duration) : string.Empty;
        FilenameHighlights = CaptureMemoryTextNormalizer.FindMatches(location.DisplayFileName, query).Ranges;
        Evidence = result.Evidence;
        MediaKind = result.MediaKind;
        CapturedAtUtc = result.CapturedAtUtc;
        FileName = location.DisplayFileName;
        CurrentFilePath = location.CurrentFilePath;
        Snippet = result.Evidence.Snippet;
        MatchKind = result.Evidence.MatchKind;
        CapturedAtLabel = result.CapturedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        CaptureTypeLabel = GetString(localizationService, $"CaptureMemory_MediaKind_{result.MediaKind}", result.MediaKind.ToString());
        ExplanationLabel = CaptureMemoryEvidenceViewModel.SourceName(result.Evidence.MatchKind, localizationService);

        TimeSpan? timecode = result.Evidence.Timecode;
        HasTimecode = timecode.HasValue;
        TimecodeLabel = timecode.HasValue ? FormatTimecode(timecode.Value) : string.Empty;
        IsImage = MediaKind == CaptureMediaKind.Image;
        IsAudio = MediaKind == CaptureMediaKind.Audio;
        IsVideo = MediaKind == CaptureMediaKind.Video;
        CanLoadThumbnail = MediaKind is CaptureMediaKind.Image or CaptureMediaKind.Video;

        CaptureMemoryPixelBounds? bounds = result.Evidence.PixelBounds;
        HasOcrBounds = result.Evidence.MatchKind == CaptureMemoryMatchKind.OcrText && bounds != null;
        OcrX = bounds?.X ?? 0;
        OcrY = bounds?.Y ?? 0;
        OcrWidth = bounds?.Width ?? 0;
        OcrHeight = bounds?.Height ?? 0;
        RasterWidth = bounds?.RasterWidth ?? 1;
        RasterHeight = bounds?.RasterHeight ?? 1;

        IsSourceMissing = location.Status == CaptureMemoryResultLocationStatus.SourceMissing;
        IsResolutionFailed = location.Status == CaptureMemoryResultLocationStatus.Unavailable;
        CanDeleteCapture = location.CanDeleteRetainedSource;
        string timecodeAutomation = HasTimecode ? $", {TimecodeLabel}" : string.Empty;
        AutomationName = $"{FileName}, {CapturedAtLabel}, {CaptureTypeLabel}, {ExplanationLabel}{timecodeAutomation}. {Snippet}";
    }

    public CaptureId CaptureId { get; }

    public CaptureMemorySearchContext SearchContext { get; }
    public ReadOnlyObservableCollection<CaptureMemoryEvidenceViewModel> EvidencePreviews { get; }
    public IReadOnlyList<CaptureTextRange> FilenameHighlights { get; }
    public int TotalMatchCount { get; }
    public bool HasMoreMatches => TotalMatchCount > EvidencePreviews.Count;
    public string ViewAllMatchesLabel { get; }
    public string DurationLabel { get; }
    public bool HasDuration => DurationLabel.Length > 0;

    public CaptureMemoryMatchEvidence Evidence { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public CaptureMemoryMatchKind MatchKind { get; }

    public string FileName { get; }

    public string CapturedAtLabel { get; }

    public string CaptureTypeLabel { get; }

    public string ExplanationLabel { get; }

    public string Snippet { get; }

    public bool HasTimecode { get; }

    public string TimecodeLabel { get; }

    public bool IsImage { get; }

    public bool IsAudio { get; }

    public bool IsVideo { get; }

    public bool CanLoadThumbnail { get; }

    public string AutomationName { get; }

    public bool HasOcrBounds { get; }

    public double OcrX { get; }

    public double OcrY { get; }

    public double OcrWidth { get; }

    public double OcrHeight { get; }

    public double RasterWidth { get; }

    public double RasterHeight { get; }

    public string? CurrentFilePath
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsSourceMissing
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanOpen));
            }
        }
    }

    public bool CanOpen => !IsSourceMissing && CurrentFilePath != null;

    public bool IsResolutionFailed { get; }

    public bool CanDeleteCapture { get; }

    public void MarkSourceMissing()
    {
        CurrentFilePath = null;
        IsSourceMissing = true;
    }

    private static string GetString(
        ILocalizationService? localizationService,
        string resourceKey,
        string fallback)
    {
        string? localized = localizationService?.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(localized) || localized == resourceKey
            ? fallback
            : localized;
    }

    private static string FormatTimecode(TimeSpan timecode)
    {
        return timecode.TotalHours >= 1
            ? timecode.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : timecode.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
