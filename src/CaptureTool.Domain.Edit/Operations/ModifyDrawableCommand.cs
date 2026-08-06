namespace CaptureTool.Domain.Edit.Operations;

public sealed class ModifyDrawableCommand : IImageEditCommand, IResolutionAwareImageEditCommand
{
    private readonly int _index;
    private ShapeState _oldState;
    private ShapeState _newState;

    public ModifyDrawableCommand(
        int index,
        ShapeState oldState,
        ShapeState newState)
    {
        _index = index;
        _oldState = oldState;
        _newState = newState;
    }

    public void Apply(ImageEditSession session)
    {
        session.ApplyShapeState(_index, _newState);
    }

    public void Revert(ImageEditSession session)
    {
        session.ApplyShapeState(_index, _oldState);
    }

    void IResolutionAwareImageEditCommand.Rebase(ImageEditSession session, double scaleX, double scaleY)
    {
        _oldState = ImageEditSession.ScaleShapeState(_oldState, scaleX, scaleY);
        _newState = ImageEditSession.ScaleShapeState(_newState, scaleX, scaleY);
    }
}
