namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record TextExtractionModelDescriptor(
    string ProducerId,
    string ModelId,
    string? ModelVersion,
    string RuntimeId,
    string? RuntimeVersion);

public readonly record struct TextExtractionRasterSize(int Width, int Height);

public readonly record struct TextExtractionPixelBounds(
    double X,
    double Y,
    double Width,
    double Height);

public sealed record TextExtractionLanguageCandidate(
    string LanguageTag,
    int Order,
    double? Confidence = null);

public sealed record TextExtractionAnalysisWord(
    string Text,
    TextExtractionPixelBounds Bounds,
    int Order,
    double? Confidence = null);

public sealed record TextExtractionAnalysisLine(
    string Text,
    TextExtractionPixelBounds Bounds,
    int Order,
    IReadOnlyList<TextExtractionAnalysisWord> Words,
    double? Confidence = null);

public sealed record TextExtractionAnalysisRegion(
    TextExtractionPixelBounds Bounds,
    int Order,
    IReadOnlyList<TextExtractionAnalysisLine> Lines,
    double? Confidence = null);

public sealed record TextExtractionAnalysisDocument(
    TextExtractionRasterSize RasterSize,
    string FullText,
    IReadOnlyList<TextExtractionLanguageCandidate> Languages,
    IReadOnlyList<TextExtractionAnalysisRegion> Regions);

public enum TextExtractionAnalysisStatus
{
    Unknown,
    Succeeded,
    Unavailable,
    Cancelled,
    TransientFailure,
    TerminalFailure,
}

public sealed record TextExtractionAnalysisResult
{
    private TextExtractionAnalysisResult(
        TextExtractionAnalysisStatus status,
        TextExtractionAnalysisDocument? document)
    {
        Status = status;
        Document = document;
    }

    public TextExtractionAnalysisStatus Status { get; }

    public TextExtractionAnalysisDocument? Document { get; }

    public static TextExtractionAnalysisResult Succeeded(TextExtractionAnalysisDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(TextExtractionAnalysisStatus.Succeeded, document);
    }

    public static TextExtractionAnalysisResult Unavailable { get; } = new(
        TextExtractionAnalysisStatus.Unavailable,
        null);

    public static TextExtractionAnalysisResult Cancelled { get; } = new(
        TextExtractionAnalysisStatus.Cancelled,
        null);

    public static TextExtractionAnalysisResult TransientFailure { get; } = new(
        TextExtractionAnalysisStatus.TransientFailure,
        null);

    public static TextExtractionAnalysisResult TerminalFailure { get; } = new(
        TextExtractionAnalysisStatus.TerminalFailure,
        null);
}

public interface ITextExtractionAnalysisService
{
    TextExtractionModelDescriptor ModelDescriptor { get; }

    TextExtractionReadyState GetReadyState();

    Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
        Stream sourceImage,
        CancellationToken cancellationToken = default);
}
