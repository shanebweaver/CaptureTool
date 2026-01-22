using System.Windows.Input;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed partial class OrientationOperation : CanvasOperation
{
    private readonly ICommand _command;
    private readonly ImageOrientation _oldOrientation;
    private readonly ImageOrientation _newOrientation;

    public OrientationOperation(ICommand command, ImageOrientation oldOrientation, ImageOrientation newOrientation)
    {
        _command = command;
        _oldOrientation = oldOrientation;
        _newOrientation = newOrientation;
    }

    public override void Undo()
    {
        _command.Execute(_oldOrientation);
    }

    public override void Redo()
    {
        _command.Execute(_newOrientation);
    }
}
