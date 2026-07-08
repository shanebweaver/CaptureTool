using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;

public sealed record ImageSuperResolutionResult(
    ImageSuperResolutionStatus Status,
    ImageFile? ImageFile = null,
    Size ImageSize = default,
    string? ErrorMessage = null)
{
    public static ImageSuperResolutionResult Success(ImageFile imageFile, Size imageSize)
    {
        return new(ImageSuperResolutionStatus.Success, imageFile, imageSize);
    }

    public static ImageSuperResolutionResult Cancelled { get; } = new(ImageSuperResolutionStatus.Cancelled);

    public static ImageSuperResolutionResult NotSupported { get; } = new(ImageSuperResolutionStatus.NotSupported);

    public static ImageSuperResolutionResult NotReady { get; } = new(ImageSuperResolutionStatus.NotReady);

    public static ImageSuperResolutionResult TooLarge(string? errorMessage = null)
    {
        return new(ImageSuperResolutionStatus.TooLarge, ErrorMessage: errorMessage);
    }

    public static ImageSuperResolutionResult Failed(string? errorMessage = null)
    {
        return new(ImageSuperResolutionStatus.Failed, ErrorMessage: errorMessage);
    }
}
