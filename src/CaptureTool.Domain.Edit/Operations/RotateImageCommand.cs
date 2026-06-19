namespace CaptureTool.Domain.Edit.Operations;

public sealed class RotateImageCommand : IImageEditCommand
{
    private readonly RotationDirection _rotationDirection;

    public RotateImageCommand(RotationDirection rotationDirection)
    {
        _rotationDirection = rotationDirection;
    }

    public void Apply(ImageEditSession session)
    {
        session.Rotate(_rotationDirection);
    }

    public void Revert(ImageEditSession session)
    {
        session.Rotate(GetOppositeDirection(_rotationDirection));
    }

    private static RotationDirection GetOppositeDirection(RotationDirection rotationDirection)
    {
        return rotationDirection == RotationDirection.Clockwise
            ? RotationDirection.CounterClockwise
            : RotationDirection.Clockwise;
    }
}
