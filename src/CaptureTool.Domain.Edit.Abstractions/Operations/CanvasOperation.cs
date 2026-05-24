namespace CaptureTool.Domain.Edit.Abstractions.Operations;

public abstract class CanvasOperation
{
    public abstract void Undo();
    public abstract void Redo();
}
