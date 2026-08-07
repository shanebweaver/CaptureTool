using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using System.Globalization;

namespace CaptureTool.Presentation.Features.Home;

public sealed class CaptureMemorySearchResultViewModel : ViewModelBase
{
    public CaptureMemorySearchResultViewModel(
        CaptureMemorySearchResult result,
        CaptureMemoryResultLocation location,
        ILocalizationService? localizationService = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        if (result.CaptureId != location.CaptureId)
        {
            throw new ArgumentException("A Memory result and its resolved location must identify the same capture.", nameof(location));
        }

        CaptureId = result.CaptureId;
        MediaKind = result.MediaKind;
        CapturedAtUtc = result.CapturedAtUtc;
        FileName = location.DisplayFileName;
        CurrentFilePath = location.CurrentFilePath;
        Snippet = result.Evidence.Snippet;
        MatchKind = result.Evidence.MatchKind;
        CapturedAtLabel = result.CapturedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        CaptureTypeLabel = GetString(localizationService, $"CaptureMemory_MediaKind_{result.MediaKind}", result.MediaKind.ToString());
        ExplanationLabel = GetString(
            localizationService,
            result.Evidence.MatchKind switch
            {
                CaptureMemoryMatchKind.OcrText => "CaptureMemory_Match_Text",
                CaptureMemoryMatchKind.ImageDescription => "CaptureMemory_Match_Visual",
                _ => "CaptureMemory_Match_Filename",
            },
            result.Evidence.MatchKind switch
            {
                CaptureMemoryMatchKind.OcrText => "Text match",
                CaptureMemoryMatchKind.ImageDescription => "Visual match",
                _ => "Filename match",
            });

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
        AutomationName = $"{FileName}, {CapturedAtLabel}, {CaptureTypeLabel}, {ExplanationLabel}. {Snippet}";
    }

    public CaptureId CaptureId { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public CaptureMemoryMatchKind MatchKind { get; }

    public string FileName { get; }

    public string CapturedAtLabel { get; }

    public string CaptureTypeLabel { get; }

    public string ExplanationLabel { get; }

    public string Snippet { get; }

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
}
