using System.Drawing;
using System.Windows.Input;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed partial class CropOperation : CanvasOperation
{
    private readonly ICommand _command;
    private readonly Rectangle _oldRectangle;
    private readonly Rectangle _newRectangle;

    public CropOperation(ICommand command, Rectangle oldRectangle, Rectangle newRectangle)
    {
        _command = command;
        _oldRectangle = oldRectangle;
        _newRectangle = newRectangle;
    }

    public override void Undo()
    {
        _command.Execute(_oldRectangle);
    }

    public override void Redo()
    {
        _command.Execute(_newRectangle);
    }
}