namespace CaptureTool.Domain.Analysis.Payloads;

public readonly record struct PixelRect
{
    public PixelRect(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel geometry must be finite.");
        }

        if (x < 0 || y < 0 || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel geometry must have a non-negative origin and positive size.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool FitsWithin(PixelSize size)
    {
        return !size.IsEmpty && X + Width <= size.Width && Y + Height <= size.Height;
    }
}

public sealed record OcrLanguageCandidateV1
{
    public OcrLanguageCandidateV1(string languageTag, double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        if (languageTag.Length > 64)
        {
            throw new ArgumentException("A language tag cannot exceed 64 characters.", nameof(languageTag));
        }

        LanguageTag = languageTag.Trim();
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public string LanguageTag { get; }

    public double? Confidence { get; }
}

public sealed record OcrWordV1
{
    public const int MaximumTextLength = 4096;

    public OcrWordV1(string text, PixelRect bounds, double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"OCR word text cannot exceed {MaximumTextLength} characters.",
                nameof(text));
        }

        if (bounds.IsEmpty)
        {
            throw new ArgumentException("An OCR word requires bounds.", nameof(bounds));
        }

        Text = text;
        Bounds = bounds;
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public string Text { get; }

    public PixelRect Bounds { get; }

    public double? Confidence { get; }
}

public sealed class OcrLineV1
{
    public const int MaximumTextLength = 65_536;
    public const int MaximumWordCount = 4096;

    public OcrLineV1(
        string text,
        PixelRect bounds,
        IEnumerable<OcrWordV1> words,
        double? confidence = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"OCR line text cannot exceed {MaximumTextLength} characters.",
                nameof(text));
        }

        if (bounds.IsEmpty)
        {
            throw new ArgumentException("An OCR line requires bounds.", nameof(bounds));
        }

        ArgumentNullException.ThrowIfNull(words);
        OcrWordV1[] copiedWords = [.. words];
        if (copiedWords.Length > MaximumWordCount)
        {
            throw new ArgumentException(
                $"An OCR line cannot exceed {MaximumWordCount} words.",
                nameof(words));
        }

        if (copiedWords.Any(word => word == null))
        {
            throw new ArgumentException("OCR words cannot contain null values.", nameof(words));
        }

        Text = text;
        Bounds = bounds;
        Words = Array.AsReadOnly(copiedWords);
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public string Text { get; }

    public PixelRect Bounds { get; }

    public IReadOnlyList<OcrWordV1> Words { get; }

    public double? Confidence { get; }

    internal bool IsEquivalentTo(OcrLineV1 other)
    {
        return string.Equals(Text, other.Text, StringComparison.Ordinal) &&
            Bounds == other.Bounds &&
            Confidence == other.Confidence &&
            Words.SequenceEqual(other.Words);
    }
}

public sealed class OcrRegionV1
{
    public const int MaximumLineCount = 4096;

    public OcrRegionV1(PixelRect bounds, IEnumerable<OcrLineV1> lines, double? confidence = null)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("An OCR region requires bounds.", nameof(bounds));
        }

        ArgumentNullException.ThrowIfNull(lines);
        OcrLineV1[] copiedLines = [.. lines];
        if (copiedLines.Length > MaximumLineCount)
        {
            throw new ArgumentException(
                $"An OCR region cannot exceed {MaximumLineCount} lines.",
                nameof(lines));
        }

        if (copiedLines.Any(line => line == null))
        {
            throw new ArgumentException("OCR lines cannot contain null values.", nameof(lines));
        }

        Bounds = bounds;
        Lines = Array.AsReadOnly(copiedLines);
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public PixelRect Bounds { get; }

    public IReadOnlyList<OcrLineV1> Lines { get; }

    public double? Confidence { get; }

    internal bool IsEquivalentTo(OcrRegionV1 other)
    {
        return Bounds == other.Bounds &&
            Confidence == other.Confidence &&
            Lines.Count == other.Lines.Count &&
            Lines.Zip(other.Lines).All(pair => pair.First.IsEquivalentTo(pair.Second));
    }
}

public sealed class OcrDocumentV1 : CapabilityPayload
{
    public const int MaximumFullTextLength = 1_000_000;
    public const int MaximumLanguageCount = 64;
    public const int MaximumRegionCount = 4096;
    public const int MaximumTotalLineCount = 50_000;
    public const int MaximumTotalWordCount = 250_000;

    public OcrDocumentV1(
        PixelSize rasterSize,
        string fullText,
        IEnumerable<OcrLanguageCandidateV1> languages,
        IEnumerable<OcrRegionV1> regions)
    {
        if (rasterSize.IsEmpty)
        {
            throw new ArgumentException("An OCR document requires positive raster dimensions.", nameof(rasterSize));
        }

        ArgumentNullException.ThrowIfNull(fullText);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(regions);
        if (fullText.Length > MaximumFullTextLength)
        {
            throw new ArgumentException(
                $"OCR full text cannot exceed {MaximumFullTextLength} characters.",
                nameof(fullText));
        }

        OcrLanguageCandidateV1[] copiedLanguages = [.. languages];
        OcrRegionV1[] copiedRegions = [.. regions];
        if (copiedLanguages.Length > MaximumLanguageCount)
        {
            throw new ArgumentException(
                $"An OCR document cannot exceed {MaximumLanguageCount} language candidates.",
                nameof(languages));
        }

        if (copiedRegions.Length > MaximumRegionCount)
        {
            throw new ArgumentException(
                $"An OCR document cannot exceed {MaximumRegionCount} regions.",
                nameof(regions));
        }

        if (copiedLanguages.Any(language => language == null))
        {
            throw new ArgumentException("OCR languages cannot contain null values.", nameof(languages));
        }

        if (copiedRegions.Any(region => region == null))
        {
            throw new ArgumentException("OCR regions cannot contain null values.", nameof(regions));
        }

        long totalLineCount = copiedRegions.Sum(region => (long)region.Lines.Count);
        long totalWordCount = copiedRegions.Sum(region =>
            region.Lines.Sum(line => (long)line.Words.Count));
        if (totalLineCount > MaximumTotalLineCount || totalWordCount > MaximumTotalWordCount)
        {
            throw new ArgumentException(
                "The OCR document exceeds its bounded line or word count.",
                nameof(regions));
        }

        ValidateGeometry(rasterSize, copiedRegions);

        RasterSize = rasterSize;
        FullText = fullText;
        Languages = Array.AsReadOnly(copiedLanguages);
        Regions = Array.AsReadOnly(copiedRegions);
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.OcrDocumentV1;

    public PixelSize RasterSize { get; }

    public string FullText { get; }

    public IReadOnlyList<OcrLanguageCandidateV1> Languages { get; }

    public IReadOnlyList<OcrRegionV1> Regions { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is OcrDocumentV1 document &&
            RasterSize == document.RasterSize &&
            string.Equals(FullText, document.FullText, StringComparison.Ordinal) &&
            Languages.SequenceEqual(document.Languages) &&
            Regions.Count == document.Regions.Count &&
            Regions.Zip(document.Regions).All(pair => pair.First.IsEquivalentTo(pair.Second));
    }

    private static void ValidateGeometry(PixelSize rasterSize, IEnumerable<OcrRegionV1> regions)
    {
        foreach (OcrRegionV1 region in regions)
        {
            if (!region.Bounds.FitsWithin(rasterSize))
            {
                throw new ArgumentException("OCR region bounds must fit within the source raster.", nameof(regions));
            }

            foreach (OcrLineV1 line in region.Lines)
            {
                if (!line.Bounds.FitsWithin(rasterSize))
                {
                    throw new ArgumentException("OCR line bounds must fit within the source raster.", nameof(regions));
                }

                if (line.Words.Any(word => !word.Bounds.FitsWithin(rasterSize)))
                {
                    throw new ArgumentException("OCR word bounds must fit within the source raster.", nameof(regions));
                }
            }
        }
    }
}
