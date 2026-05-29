using System.Drawing;

namespace CaptureTool.Domain.Edit.Abstractions.Operations;

public sealed partial class CropOperation : CanvasOperation
{
    private readonly Action<Rectangle> _action;
    private readonly Rectangle _oldRectangle;
    private readonly Rectangle _newRectangle;

    public CropOperation(
        Action<Rectangle> action,
        Rectangle oldRectangle,
        Rectangle newRectangle)
    {
        _action = action;
        _oldRectangle = oldRectangle;
        _newRectangle = newRectangle;
    }

    public override void Undo()
    {
        _action(_oldRectangle);
    }

    public override void Redo()
    {
        _action(_newRectangle);
    }
}