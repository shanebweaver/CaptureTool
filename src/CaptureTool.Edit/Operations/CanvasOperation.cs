namespace CaptureTool.Edit.Operations;

public abstract class CanvasOperation
{
    public abstract void Undo();
    public abstract void Redo();
}
