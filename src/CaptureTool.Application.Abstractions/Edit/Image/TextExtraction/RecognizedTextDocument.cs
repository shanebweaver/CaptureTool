using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedTextDocument
{
    public RecognizedTextDocument(
        string text,
        Size imageSize,
        IReadOnlyList<RecognizedTextRegion> regions,
        IReadOnlyList<RecognizedQrCodeRegion>? qrCodes = null,
        bool hasTextResults = true)
    {
        Text = text;
        ImageSize = imageSize;
        Regions = regions;
        QrCodes = qrCodes ?? [];
        HasQrCodeResults = qrCodes != null;
        HasTextResults = hasTextResults;
    }

    public string Text { get; }

    public Size ImageSize { get; }

    /// <summary>OCR regions; an empty bound preserves copyable text whose position is unknown.</summary>
    public IReadOnlyList<RecognizedTextRegion> Regions { get; }

    /// <summary>False means OCR is missing, even if QR results are available.</summary>
    public bool HasTextResults { get; }

    public IReadOnlyList<RecognizedQrCodeRegion> QrCodes { get; }

    /// <summary>Distinguishes a completed empty QR scan from metadata that predates QR scanning.</summary>
    public bool HasQrCodeResults { get; }

    /// <summary>Combines OCR text with decoded QR values for copying, without adding QR values to word regions.</summary>
    public static RecognizedTextDocument FromRecognition(
        string recognizedText,
        Size imageSize,
        IReadOnlyList<RecognizedTextRegion> regions,
        IReadOnlyList<RecognizedQrCodeRegion>? qrCodes,
        bool hasTextResults = true)
    {
        IEnumerable<string> values = qrCodes?.Select(code => code.Value) ?? [];
        if (!string.IsNullOrWhiteSpace(recognizedText)) values = values.Prepend(recognizedText.TrimEnd());
        return new(string.Join(Environment.NewLine, values), imageSize, regions, qrCodes, hasTextResults);
    }
}

