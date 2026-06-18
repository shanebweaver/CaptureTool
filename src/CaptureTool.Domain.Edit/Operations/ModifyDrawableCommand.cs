namespace CaptureTool.Domain.Edit.Operations;

public sealed class ModifyDrawableCommand : IImageEditCommand
{
    private readonly int _index;
    private readonly ModifyShapeOperation.ShapeState _oldState;
    private readonly ModifyShapeOperation.ShapeState _newState;

    public ModifyDrawableCommand(
        int index,
        ModifyShapeOperation.ShapeState oldState,
        ModifyShapeOperation.ShapeState newState)
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
}
