namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public sealed record ImageDescriptionPreparationResult(
    ImageDescriptionPreparationStatus Status,
    string? ErrorMessage = null)
{
    public static ImageDescriptionPreparationResult Success { get; } = new(ImageDescriptionPreparationStatus.Success);

    public static ImageDescriptionPreparationResult Cancelled { get; } = new(ImageDescriptionPreparationStatus.Cancelled);

    public static ImageDescriptionPreparationResult NotSupported { get; } = new(ImageDescriptionPreparationStatus.NotSupported);

    public static ImageDescriptionPreparationResult Failed(string? errorMessage = null)
    {
        return new(ImageDescriptionPreparationStatus.Failed, errorMessage);
    }
}
