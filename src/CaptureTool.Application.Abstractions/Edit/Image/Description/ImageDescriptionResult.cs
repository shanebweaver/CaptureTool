namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public sealed record ImageDescriptionResult(
    ImageDescriptionStatus Status,
    string Description = "",
    string? ErrorMessage = null)
{
    public static ImageDescriptionResult Success(string description)
    {
        return new(ImageDescriptionStatus.Success, description);
    }

    public static ImageDescriptionResult Cancelled { get; } = new(ImageDescriptionStatus.Cancelled);

    public static ImageDescriptionResult NotReady { get; } = new(ImageDescriptionStatus.NotReady);

    public static ImageDescriptionResult NotSupported { get; } = new(ImageDescriptionStatus.NotSupported);

    public static ImageDescriptionResult BlockedByPolicy { get; } = new(ImageDescriptionStatus.BlockedByPolicy);

    public static ImageDescriptionResult BlockedByContentSafety { get; } = new(ImageDescriptionStatus.BlockedByContentSafety);

    public static ImageDescriptionResult TooMuchText { get; } = new(ImageDescriptionStatus.TooMuchText);

    public static ImageDescriptionResult Failed(string? errorMessage = null)
    {
        return new(ImageDescriptionStatus.Failed, ErrorMessage: errorMessage);
    }
}
