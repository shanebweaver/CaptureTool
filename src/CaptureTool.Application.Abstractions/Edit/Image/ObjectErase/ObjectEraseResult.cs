using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;

public sealed record ObjectEraseResult(
    ObjectEraseStatus Status,
    ImageFile? ImageFile = null,
    string? ErrorMessage = null)
{
    public static ObjectEraseResult Success(ImageFile imageFile)
    {
        return new(ObjectEraseStatus.Success, imageFile);
    }

    public static ObjectEraseResult Cancelled { get; } = new(ObjectEraseStatus.Cancelled);

    public static ObjectEraseResult NotReady { get; } = new(ObjectEraseStatus.NotReady);

    public static ObjectEraseResult NotSupported { get; } = new(ObjectEraseStatus.NotSupported);

    public static ObjectEraseResult Failed(string? errorMessage = null)
    {
        return new(ObjectEraseStatus.Failed, ErrorMessage: errorMessage);
    }
}
