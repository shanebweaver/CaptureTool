namespace CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;

public sealed record ImageSuperResolutionPreparationResult(
    ImageSuperResolutionPreparationStatus Status,
    string? ErrorMessage = null)
{
    public static ImageSuperResolutionPreparationResult Success { get; } = new(ImageSuperResolutionPreparationStatus.Success);

    public static ImageSuperResolutionPreparationResult Cancelled { get; } = new(ImageSuperResolutionPreparationStatus.Cancelled);

    public static ImageSuperResolutionPreparationResult NotSupported { get; } = new(ImageSuperResolutionPreparationStatus.NotSupported);

    public static ImageSuperResolutionPreparationResult Failed(string? errorMessage = null)
    {
        return new(ImageSuperResolutionPreparationStatus.Failed, errorMessage);
    }
}
