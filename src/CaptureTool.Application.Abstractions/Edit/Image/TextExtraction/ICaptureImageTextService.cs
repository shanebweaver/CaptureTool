using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

/// <summary>Shares saved-image OCR and QR results with the capture metadata pipeline.</summary>
public interface ICaptureImageTextService
{
    /// <summary>
    /// Reuses or persists recognition for an unedited image. Returns null when the working
    /// image cannot be matched to its source; the editor may then scan its rendered canvas.
    /// Cancellation stops only the analysis request admitted by this call.
    /// </summary>
    Task<TextExtractionResult?> TryExtractAsync(ImageFile image, Size imageSize, CancellationToken cancellationToken = default);
}
