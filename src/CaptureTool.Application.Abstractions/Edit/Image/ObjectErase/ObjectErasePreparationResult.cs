namespace CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;

public sealed record ObjectErasePreparationResult(
    ObjectErasePreparationStatus Status,
    string? ErrorMessage = null)
{
    public static ObjectErasePreparationResult Success { get; } = new(ObjectErasePreparationStatus.Success);

    public static ObjectErasePreparationResult Cancelled { get; } = new(ObjectErasePreparationStatus.Cancelled);

    public static ObjectErasePreparationResult NotSupported { get; } = new(ObjectErasePreparationStatus.NotSupported);

    public static ObjectErasePreparationResult Failed(string? errorMessage = null)
    {
        return new(ObjectErasePreparationStatus.Failed, errorMessage);
    }
}
