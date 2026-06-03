namespace CaptureTool.Domain.Edit.Operations;

public sealed partial class OrientationOperation : CanvasOperation
{
    private readonly Action<ImageOrientation> _action;
    private readonly ImageOrientation _oldOrientation;
    private readonly ImageOrientation _newOrientation;

    public OrientationOperation(
        Action<ImageOrientation> action,
        ImageOrientation oldOrientation,
        ImageOrientation newOrientation)
    {
        _action = action;
        _oldOrientation = oldOrientation;
        _newOrientation = newOrientation;
    }

    public override void Undo()
    {
        _action(_oldOrientation);
    }

    public override void Redo()
    {
        _action(_newOrientation);
    }
}
