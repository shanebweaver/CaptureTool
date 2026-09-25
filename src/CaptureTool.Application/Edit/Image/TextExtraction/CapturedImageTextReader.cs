using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;
using System.Security.Cryptography;

namespace CaptureTool.Application.Edit.Image.TextExtraction;

internal sealed class CapturedImageTextReader(ICaptureAssetCatalog catalog, ICaptureMetadataReader metadata, IAnalysisSource files,
    IRecognizedTextDocumentBuilder documents)
    : ICapturedImageTextReader
{
    public async Task<CapturedImageTextSource?> OpenAsync(ImageFile image, Size size, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(image.FilePath) || size.Width <= 0 || size.Height <= 0) return null;
        try
        {
            await using var lease = await files.OpenAsync(image.FilePath, cancellationToken).ConfigureAwait(false);
            return new(image.FilePath, image.PersistentFilePath, lease.Revision, size);
        }
        catch (Exception ex) when (CacheUnavailable(ex)) { return null; }
    }

    public async Task<RecognizedTextDocument?> ReadAsync(CapturedImageTextSource source, CancellationToken cancellationToken)
    {
        try
        {
            var matches = (await catalog.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                .Where(asset => asset.MediaType == CaptureFileType.Image &&
                    (SamePath(asset.SourcePath, source.Path) || SamePath(asset.PreferredPath, source.Path) ||
                     SamePath(asset.SourcePath, source.PersistentPath) || SamePath(asset.PreferredPath, source.PersistentPath)))
                .Take(2).ToArray();
            if (matches.Length != 1) return null;
            await using var lease = await files.OpenAsync(source.Path, cancellationToken).ConfigureAwait(false);
            if (lease.Revision != source.Revision) return null;
            var record = await metadata.GetAsync(matches[0].Id, source.Revision, cancellationToken).ConfigureAwait(false);
            if (record?.CaptureId != matches[0].Id || record.MediaKind != AnalysisMediaKind.Image || record.SourceRevision != source.Revision ||
                !await lease.VerifyAsync(cancellationToken).ConfigureAwait(false)) return null;
            var text = record.Results.SingleOrDefault(result => result.Payload is TextRecognitionMetadata)?.Payload as TextRecognitionMetadata;
            var qr = record.Results.SingleOrDefault(result => result.Payload is QrCodeMetadata)?.Payload as QrCodeMetadata;
            if (text == null && qr == null) return null;
            RecognizedTextRegion[]? regions = text?.Regions.Where(region => region.Timestamp == null)
                .Select(region => new RecognizedTextRegion(region.Text, region.Bounds is { } bounds ? new RectangleF(
                    (float)(bounds.X * source.ImageSize.Width), (float)(bounds.Y * source.ImageSize.Height),
                    (float)(bounds.Width * source.ImageSize.Width), (float)(bounds.Height * source.ImageSize.Height)) : RectangleF.Empty))
                .ToArray();
            // Empty OCR is a valid cached success and must not trigger another model run.
            RecognizedQrCodeRegion[]? codes = qr?.Codes.Where(code => code.Timestamp == null)
                .Select(code => new RecognizedQrCodeRegion(code.Value, new RectangleF(
                    (float)(code.Bounds.X * source.ImageSize.Width), (float)(code.Bounds.Y * source.ImageSize.Height),
                    (float)(code.Bounds.Width * source.ImageSize.Width), (float)(code.Bounds.Height * source.ImageSize.Height))))
                .ToArray();
            return documents.Build(source.ImageSize, regions, codes);
        }
        catch (Exception ex) when (CacheUnavailable(ex)) { return null; }
    }

    private static bool SamePath(string? first, string? second) => first != null && second != null &&
        Path.IsPathFullyQualified(first) && Path.IsPathFullyQualified(second) &&
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static bool CacheUnavailable(Exception ex) => ex is IOException or UnauthorizedAccessException or CryptographicException;
}
