namespace CaptureTool.Domain.Edit.Operations;

public sealed class SetOrientationCommand : IImageEditCommand
{
    private readonly ImageOrientation _oldOrientation;
    private readonly ImageOrientation _newOrientation;

    public SetOrientationCommand(ImageOrientation oldOrientation, ImageOrientation newOrientation)
    {
        _oldOrientation = oldOrientation;
        _newOrientation = newOrientation;
    }

    public void Apply(ImageEditSession session)
    {
        session.SetOrientation(_newOrientation);
    }

    public void Revert(ImageEditSession session)
    {
        session.SetOrientation(_oldOrientation);
    }
}
