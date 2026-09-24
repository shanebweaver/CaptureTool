using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Memory;

public sealed record CaptureMemorySearchRequest
{
    public const int MaximumQueryLength = 1024;
    public const int MaximumResultLimit = 200;

    public CaptureMemorySearchRequest(string query, int maximumResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length > MaximumQueryLength)
        {
            throw new ArgumentException(
                $"A Memory query cannot exceed {MaximumQueryLength} characters.",
                nameof(query));
        }

        if (maximumResults is <= 0 or > MaximumResultLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        Query = normalizedQuery;
        MaximumResults = maximumResults;
    }

    public string Query { get; }

    public int MaximumResults { get; }
}

public enum CaptureMemoryMatchKind
{
    Unknown,
    Filename,
    OcrText,
    ImageDescription,
    SpeechTranscript,
    VideoOcrText,
    VideoDescription,
}

public sealed record CaptureMemoryPixelBounds
{
    public CaptureMemoryPixelBounds(
        double x,
        double y,
        double width,
        double height,
        int rasterWidth,
        int rasterHeight)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(width) || !double.IsFinite(height) ||
            x < 0 || y < 0 || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Memory match geometry must be finite and positive.");
        }

        if (rasterWidth <= 0 || rasterHeight <= 0 ||
            x + width > rasterWidth || y + height > rasterHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rasterWidth),
                "Memory match geometry must fit inside a positive source raster.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
        RasterWidth = rasterWidth;
        RasterHeight = rasterHeight;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public int RasterWidth { get; }

    public int RasterHeight { get; }
}

public sealed record CaptureMemoryMatchEvidence
{
    public const int MaximumSnippetLength = 1024;

    public CaptureMemoryMatchEvidence(
        CaptureMemoryMatchKind matchKind,
        string snippet,
        CaptureMemoryPixelBounds? pixelBounds = null,
        TimeSpan? timecode = null,
        string? evidenceId = null,
        TimeSpan? endTime = null,
        IReadOnlyList<CaptureTextRange>? highlights = null,
        bool isApproximate = false,
        bool isCombinedMatch = false)
    {
        if (!Enum.IsDefined(matchKind) || matchKind == CaptureMemoryMatchKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(matchKind));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(snippet);
        string normalizedSnippet = snippet.Trim();
        if (normalizedSnippet.Length > MaximumSnippetLength)
        {
            throw new ArgumentException(
                $"Memory match evidence cannot exceed {MaximumSnippetLength} characters.",
                nameof(snippet));
        }

        if (timecode < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timecode));
        }

        if (endTime.HasValue && (!timecode.HasValue || endTime < timecode))
        {
            throw new ArgumentOutOfRangeException(nameof(endTime));
        }
        if (evidenceId is { Length: > 128 } || evidenceId is { Length: 0 })
        {
            throw new ArgumentException("An evidence identity must be bounded and nonempty.", nameof(evidenceId));
        }
        CaptureTextRange[] ranges = [.. highlights ?? []];
        if (ranges.Any(range => range.Start < 0 || range.Length <= 0 ||
            (long)range.Start + range.Length > normalizedSnippet.Length))
        {
            throw new ArgumentException("Highlights must fit inside the snippet.", nameof(highlights));
        }

        MatchKind = matchKind;
        Snippet = normalizedSnippet;
        PixelBounds = pixelBounds;
        Timecode = timecode;
        EvidenceId = evidenceId;
        EndTime = endTime;
        Highlights = Array.AsReadOnly(ranges);
        IsApproximate = isApproximate;
        IsCombinedMatch = isCombinedMatch;
    }

    public CaptureMemoryMatchKind MatchKind { get; }

    public string Snippet { get; }

    public CaptureMemoryPixelBounds? PixelBounds { get; }

    public TimeSpan? Timecode { get; }

    public string? EvidenceId { get; }

    public TimeSpan? EndTime { get; }

    public IReadOnlyList<CaptureTextRange> Highlights { get; }

    public bool IsApproximate { get; }

    public bool IsCombinedMatch { get; }
}

public sealed record CaptureMemorySearchResult
{
    public const int MaximumEvidenceCount = 200;

    public CaptureMemorySearchResult(
        CaptureId captureId,
        CaptureMediaKind mediaKind,
        DateTimeOffset capturedAtUtc,
        double score,
        int rank,
        CaptureMemoryMatchEvidence evidence,
        IReadOnlyList<CaptureMemoryMatchEvidence>? matches = null,
        int? totalMatchCount = null,
        long? documentRevision = null,
        SourceRevision? sourceRevision = null,
        TimeSpan? duration = null)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A Memory result requires a capture ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A captured timestamp must be expressed in UTC.", nameof(capturedAtUtc));
        }

        if (!double.IsFinite(score) || score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        CaptureMemoryMatchEvidence[] evidenceItems = [.. matches ?? [evidence]];
        if (evidenceItems.Length is 0 or > MaximumEvidenceCount || !evidenceItems.Contains(evidence))
        {
            throw new ArgumentException("Evidence must include the primary match and fit within the result limit.", nameof(matches));
        }
        if (totalMatchCount < evidenceItems.Length || documentRevision <= 0 || duration < TimeSpan.Zero ||
            sourceRevision is { IsEmpty: true })
        {
            throw new ArgumentOutOfRangeException(nameof(totalMatchCount));
        }

        CaptureId = captureId;
        MediaKind = mediaKind;
        CapturedAtUtc = capturedAtUtc;
        Score = score;
        Rank = rank;
        Evidence = evidence;
        Matches = Array.AsReadOnly(evidenceItems);
        TotalMatchCount = totalMatchCount ?? evidenceItems.Length;
        DocumentRevision = documentRevision;
        SourceRevision = sourceRevision;
        Duration = duration;
    }

    public CaptureId CaptureId { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public double Score { get; }

    public int Rank { get; }

    public CaptureMemoryMatchEvidence Evidence { get; }

    public IReadOnlyList<CaptureMemoryMatchEvidence> Matches { get; }

    public int TotalMatchCount { get; }

    public long? DocumentRevision { get; }

    public SourceRevision? SourceRevision { get; }

    public TimeSpan? Duration { get; }
}

/// <summary>Transient search context; never persisted with metadata or sent to diagnostics.</summary>
public sealed record CaptureMemorySearchContext
{
    public CaptureMemorySearchContext(string query, long? documentRevision = null, SourceRevision? sourceRevision = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length > CaptureMemorySearchRequest.MaximumQueryLength || documentRevision <= 0 ||
            sourceRevision is { IsEmpty: true })
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }
        Query = query.Trim();
        DocumentRevision = documentRevision;
        SourceRevision = sourceRevision;
    }

    public string Query { get; }

    public long? DocumentRevision { get; }

    public SourceRevision? SourceRevision { get; }
}

public interface ICaptureMemorySearchService
{
    ValueTask<IReadOnlyList<CaptureMemorySearchResult>> SearchAsync(
        CaptureMemorySearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICaptureMemoryFeatureAvailability
{
    bool IsCaptureMemorySearchEnabled { get; }
}

public interface ICaptureMemorySearchChangeNotifier
{
    event EventHandler? SearchIndexChanged;
}
