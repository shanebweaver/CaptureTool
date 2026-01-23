using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed partial class OrientationOperation : CanvasOperation
{
    private readonly IAppCommand<ImageOrientation> _command;
    private readonly ImageOrientation _oldOrientation;
    private readonly ImageOrientation _newOrientation;

    public OrientationOperation(IAppCommand<ImageOrientation> command, ImageOrientation oldOrientation, ImageOrientation newOrientation)
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
