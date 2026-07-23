using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;

public sealed record ForegroundExtractionResult(
    ForegroundExtractionStatus Status,
    ImageFile? ImageFile = null,
    string? ErrorMessage = null)
{
    public static ForegroundExtractionResult Success(ImageFile imageFile)
    {
        return new(ForegroundExtractionStatus.Success, imageFile);
    }

    public static ForegroundExtractionResult Cancelled { get; } = new(ForegroundExtractionStatus.Cancelled);

    public static ForegroundExtractionResult NotReady { get; } = new(ForegroundExtractionStatus.NotReady);

    public static ForegroundExtractionResult NotSupported { get; } = new(ForegroundExtractionStatus.NotSupported);

    public static ForegroundExtractionResult Failed(string? errorMessage = null)
    {
        return new(ForegroundExtractionStatus.Failed, ErrorMessage: errorMessage);
    }
}
