using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record RecognizedTextDocument
{
    public RecognizedTextDocument(
        string text,
        Size imageSize,
        IReadOnlyList<RecognizedTextRegion> regions,
        IReadOnlyList<RecognizedQrCodeRegion>? qrCodes = null)
    {
        Text = text;
        ImageSize = imageSize;
        Regions = regions;
        QrCodes = qrCodes ?? [];
        HasQrCodeResults = qrCodes != null;
    }

    public string Text { get; }

    public Size ImageSize { get; }

    public IReadOnlyList<RecognizedTextRegion> Regions { get; }

    public IReadOnlyList<RecognizedQrCodeRegion> QrCodes { get; }

    /// <summary>Distinguishes a completed empty QR scan from metadata that predates QR scanning.</summary>
    public bool HasQrCodeResults { get; }
}

