namespace CaptureTool.Domain.Edit.Operations;

public sealed class ImageEditHistory
{
    private readonly Stack<HistoryEntry> _undoStack = [];
    private readonly Stack<HistoryEntry> _redoStack = [];

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(ImageEditSession session, IImageEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);

        command.Apply(session);
        _undoStack.Push(new HistoryEntry(command, session.ImageSize));
        _redoStack.Clear();
    }

    public bool Undo(ImageEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_undoStack.Count == 0)
        {
            return false;
        }

        HistoryEntry entry = _undoStack.Pop();
        entry.Rebase(session);
        entry.Command.Revert(session);
        _redoStack.Push(entry);
        return true;
    }

    public bool Redo(ImageEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_redoStack.Count == 0)
        {
            return false;
        }

        HistoryEntry entry = _redoStack.Pop();
        entry.Rebase(session);
        entry.Command.Apply(session);
        _undoStack.Push(entry);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private sealed class HistoryEntry(IImageEditCommand command, System.Drawing.Size imageSize)
    {
        public IImageEditCommand Command { get; } = command;

        private System.Drawing.Size ImageSize { get; set; } = imageSize;

        public void Rebase(ImageEditSession session)
        {
            System.Drawing.Size currentSize = session.ImageSize;
            if (currentSize == ImageSize)
            {
                return;
            }

            if (Command is IResolutionAwareImageEditCommand resolutionAwareCommand &&
                ImageSize.Width > 0 &&
                ImageSize.Height > 0 &&
                currentSize.Width > 0 &&
                currentSize.Height > 0)
            {
                double scaleX = (double)currentSize.Width / ImageSize.Width;
                double scaleY = (double)currentSize.Height / ImageSize.Height;
                resolutionAwareCommand.Rebase(session, scaleX, scaleY);
            }

            ImageSize = currentSize;
        }
    }
}
