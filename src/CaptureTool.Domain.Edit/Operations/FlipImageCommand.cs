namespace CaptureTool.Domain.Edit.Operations;

public sealed class FlipImageCommand : IImageEditCommand
{
    private readonly FlipDirection _flipDirection;

    public FlipImageCommand(FlipDirection flipDirection)
    {
        _flipDirection = flipDirection;
    }

    public void Apply(ImageEditSession session)
    {
        session.Flip(_flipDirection);
    }

    public void Revert(ImageEditSession session)
    {
        session.Flip(_flipDirection);
    }
}
