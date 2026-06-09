using System.Drawing;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class ChromaKeyOperation : CanvasOperation
{
    private readonly Action<ChromaKeyState> _action;
    private readonly ChromaKeyState _oldState;
    private readonly ChromaKeyState _newState;

    public ChromaKeyOperation(
        Action<ChromaKeyState> action,
        ChromaKeyState oldState,
        ChromaKeyState newState)
    {
        _action = action;
        _oldState = oldState;
        _newState = newState;
    }

    public override void Undo()
    {
        _action(_oldState);
    }

    public override void Redo()
    {
        _action(_newState);
    }

    public readonly record struct ChromaKeyState(
        int SelectedColorOptionIndex,
        Color Color,
        int Tolerance,
        int Desaturation);
}
