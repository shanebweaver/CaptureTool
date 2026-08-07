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
        TimeSpan? timecode = null)
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

        MatchKind = matchKind;
        Snippet = normalizedSnippet;
        PixelBounds = pixelBounds;
        Timecode = timecode;
    }

    public CaptureMemoryMatchKind MatchKind { get; }

    public string Snippet { get; }

    public CaptureMemoryPixelBounds? PixelBounds { get; }

    public TimeSpan? Timecode { get; }
}

public sealed record CaptureMemorySearchResult
{
    public CaptureMemorySearchResult(
        CaptureId captureId,
        CaptureMediaKind mediaKind,
        DateTimeOffset capturedAtUtc,
        double score,
        int rank,
        CaptureMemoryMatchEvidence evidence)
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

        CaptureId = captureId;
        MediaKind = mediaKind;
        CapturedAtUtc = capturedAtUtc;
        Score = score;
        Rank = rank;
        Evidence = evidence;
    }

    public CaptureId CaptureId { get; }

    public CaptureMediaKind MediaKind { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public double Score { get; }

    public int Rank { get; }

    public CaptureMemoryMatchEvidence Evidence { get; }
}

public interface ICaptureMemorySearchService
{
    ValueTask<IReadOnlyList<CaptureMemorySearchResult>> SearchAsync(
        CaptureMemorySearchRequest request,
        CancellationToken cancellationToken = default);
}
