namespace CaptureTool.Domain.Edit.Operations;

public sealed class ImageEditHistory
{
    private readonly Stack<IImageEditCommand> _undoStack = [];
    private readonly Stack<IImageEditCommand> _redoStack = [];

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(ImageEditSession session, IImageEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);

        command.Apply(session);
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public bool Undo(ImageEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_undoStack.Count == 0)
        {
            return false;
        }

        IImageEditCommand command = _undoStack.Pop();
        command.Revert(session);
        _redoStack.Push(command);
        return true;
    }

    public bool Redo(ImageEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_redoStack.Count == 0)
        {
            return false;
        }

        IImageEditCommand command = _redoStack.Pop();
        command.Apply(session);
        _undoStack.Push(command);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
