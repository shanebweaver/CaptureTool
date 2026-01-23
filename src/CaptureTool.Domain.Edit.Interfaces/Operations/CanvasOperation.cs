namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public abstract class CanvasOperation
{
    public abstract void Undo();
    public abstract void Redo();
}
