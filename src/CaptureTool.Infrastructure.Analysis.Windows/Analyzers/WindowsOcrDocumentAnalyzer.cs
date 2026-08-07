using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsOcrDocumentAnalyzer : ICaptureAnalyzer
{
    public const string AdapterVersion = "1.0.0";

    private readonly ITextExtractionAnalysisService _textExtraction;

    public WindowsOcrDocumentAnalyzer(ITextExtractionAnalysisService textExtraction)
    {
        ArgumentNullException.ThrowIfNull(textExtraction);
        _textExtraction = textExtraction;

        TextExtractionModelDescriptor model = textExtraction.ModelDescriptor;
        var identity = new AnalyzerIdentity(
            analyzerId: "windows-ocr-document",
            providerId: model.ProducerId,
            modelId: model.ModelId,
            modelVersion: model.ModelVersion,
            adapterVersion: AdapterVersion,
            runtimeId: model.RuntimeId,
            runtimeVersion: model.RuntimeVersion,
            packageVersion: null,
            configurationFingerprint: null);
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.OcrDocumentV1,
            identity,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.OperatingSystemCapability,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes: null,
            qualityTier: 100);
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
        CaptureAnalyzerAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The availability request targets another analyzer.", nameof(request));
        }

        try
        {
            CaptureAnalyzerAvailability availability = _textExtraction.GetReadyState() switch
            {
                TextExtractionReadyState.Ready => CaptureAnalyzerAvailability.Available,
                TextExtractionReadyState.PreparationNeeded =>
                    CaptureAnalyzerAvailability.PreparationRequired,
                TextExtractionReadyState.Disabled => CaptureAnalyzerAvailability.Disabled,
                TextExtractionReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
                    new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                _ => CaptureAnalyzerAvailability.TemporarilyUnavailable(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
            };
            return ValueTask.FromResult(availability);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ValueTask.FromResult(CaptureAnalyzerAvailability.TemporarilyUnavailable(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)));
        }
    }

    public async Task<CaptureAnalyzerOutput> AnalyzeAsync(
        CaptureAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The analysis request targets another analyzer.", nameof(request));
        }

        try
        {
            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);
            TextExtractionAnalysisResult result = await _textExtraction.ExtractAnalysisAsync(
                source,
                cancellationToken).ConfigureAwait(false);

            return result.Status switch
            {
                TextExtractionAnalysisStatus.Succeeded => CreateSuccessfulOutput(result.Document),
                TextExtractionAnalysisStatus.Unavailable => CaptureAnalyzerOutput.Unsupported(
                    new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                TextExtractionAnalysisStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
                TextExtractionAnalysisStatus.TransientFailure => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
                TextExtractionAnalysisStatus.TerminalFailure => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.InvalidResponse,
                        AnalysisFailureDisposition.Terminal)),
                _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                    AnalysisFailureCode.InvalidResponse,
                    AnalysisFailureDisposition.Terminal)),
            };
        }
        catch (OperationCanceledException)
        {
            return CaptureAnalyzerOutput.Cancelled;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidSource,
                AnalysisFailureDisposition.Terminal));
        }
        catch
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient));
        }
    }

    private static CaptureAnalyzerOutput CreateSuccessfulOutput(
        TextExtractionAnalysisDocument? document)
    {
        if (document is null)
        {
            return InvalidResponse();
        }

        try
        {
            return CaptureAnalyzerOutput.Succeeded(Normalize(document));
        }
        catch (Exception exception) when (exception is
            ArgumentException or ArithmeticException or InvalidOperationException)
        {
            return InvalidResponse();
        }
    }

    private static OcrDocumentV1 Normalize(TextExtractionAnalysisDocument document)
    {
        var rasterSize = new PixelSize(
            document.RasterSize.Width,
            document.RasterSize.Height);
        string fullText = NormalizeFullText(document.FullText);

        TextExtractionLanguageCandidate[] languageSource =
            document.Languages?.ToArray() ??
            throw new ArgumentException("OCR languages cannot be null.", nameof(document));
        OcrLanguageCandidateV1[] languages = [.. languageSource
            .Select((language, index) => (Language: language, Index: index))
            .OrderBy(item => item.Language.Order)
            .ThenBy(item => item.Index)
            .Select(item =>
            {
                EnsureValidOrder(item.Language.Order);
                return new OcrLanguageCandidateV1(
                    NormalizeRequiredText(item.Language.LanguageTag),
                    item.Language.Confidence);
            })];

        TextExtractionAnalysisRegion[] regionSource =
            document.Regions?.ToArray() ??
            throw new ArgumentException("OCR regions cannot be null.", nameof(document));
        OcrRegionV1[] regions = [.. regionSource
            .Select((region, index) => (Region: region, Index: index))
            .OrderBy(item => item.Region.Order)
            .ThenBy(item => item.Index)
            .Select(item => NormalizeRegion(item.Region, rasterSize))];

        return new OcrDocumentV1(rasterSize, fullText, languages, regions);
    }

    private static OcrRegionV1 NormalizeRegion(
        TextExtractionAnalysisRegion region,
        PixelSize rasterSize)
    {
        ArgumentNullException.ThrowIfNull(region);
        EnsureValidOrder(region.Order);
        TextExtractionAnalysisLine[] lineSource = region.Lines?.ToArray() ??
            throw new ArgumentException("OCR lines cannot be null.", nameof(region));
        OcrLineV1[] lines = [.. lineSource
            .Select((line, index) => (Line: line, Index: index))
            .OrderBy(item => item.Line.Order)
            .ThenBy(item => item.Index)
            .Select(item => NormalizeLine(item.Line, rasterSize))];

        return new OcrRegionV1(
            NormalizeBounds(region.Bounds, rasterSize),
            lines,
            region.Confidence);
    }

    private static OcrLineV1 NormalizeLine(
        TextExtractionAnalysisLine line,
        PixelSize rasterSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureValidOrder(line.Order);
        TextExtractionAnalysisWord[] wordSource = line.Words?.ToArray() ??
            throw new ArgumentException("OCR words cannot be null.", nameof(line));
        OcrWordV1[] words = [.. wordSource
            .Select((word, index) => (Word: word, Index: index))
            .OrderBy(item => item.Word.Order)
            .ThenBy(item => item.Index)
            .Select(item => NormalizeWord(item.Word, rasterSize))];

        return new OcrLineV1(
            NormalizeOptionalText(line.Text),
            NormalizeBounds(line.Bounds, rasterSize),
            words,
            line.Confidence);
    }

    private static OcrWordV1 NormalizeWord(
        TextExtractionAnalysisWord word,
        PixelSize rasterSize)
    {
        ArgumentNullException.ThrowIfNull(word);
        EnsureValidOrder(word.Order);
        return new OcrWordV1(
            NormalizeRequiredText(word.Text),
            NormalizeBounds(word.Bounds, rasterSize),
            word.Confidence);
    }

    private static PixelRect NormalizeBounds(
        TextExtractionPixelBounds bounds,
        PixelSize rasterSize)
    {
        var normalized = new PixelRect(
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);
        if (!normalized.FitsWithin(rasterSize))
        {
            throw new ArgumentException("OCR bounds must fit within the orientation-corrected raster.");
        }

        return normalized;
    }

    private static string NormalizeFullText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n')
            .Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeOptionalText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeRequiredText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return text.Trim().Normalize(NormalizationForm.FormC);
    }

    private static void EnsureValidOrder(int order)
    {
        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    private static CaptureAnalyzerOutput InvalidResponse()
    {
        return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
            AnalysisFailureCode.InvalidResponse,
            AnalysisFailureDisposition.Terminal));
    }
}
