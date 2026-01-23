using CaptureTool.Infrastructure.Interfaces.Commands;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed partial class CropOperation : CanvasOperation
{
    private readonly IAppCommand<Rectangle> _command;
    private readonly Rectangle _oldRectangle;
    private readonly Rectangle _newRectangle;

    public CropOperation(IAppCommand<Rectangle> command, Rectangle oldRectangle, Rectangle newRectangle)
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