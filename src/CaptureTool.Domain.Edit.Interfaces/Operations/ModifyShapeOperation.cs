using CaptureTool.Domain.Edit.Interfaces.Drawable;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed class ModifyShapeOperation : CanvasOperation
{
    private readonly IDrawable _shape;
    private readonly ShapeState _oldState;
    private readonly ShapeState _newState;
    private readonly Action? _invalidateCallback;

    public ModifyShapeOperation(IDrawable shape, ShapeState oldState, ShapeState newState, Action? invalidateCallback = null)
    {
        _shape = shape;
        _oldState = oldState;
        _newState = newState;
        _invalidateCallback = invalidateCallback;
    }

    public override void Undo()
    {
        ApplyState(_oldState);
        _invalidateCallback?.Invoke();
    }

    public override void Redo()
    {
        ApplyState(_newState);
        _invalidateCallback?.Invoke();
    }

    private void ApplyState(ShapeState state)
    {
        switch (_shape)
        {
            case RectangleDrawable rect:
                rect.Offset = state.Offset;
                rect.Size = state.Size;
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = state.Offset;
                ellipse.Size = state.Size;
                break;

            case LineDrawable line:
                line.Offset = state.Offset;
                line.EndPoint = state.EndPoint;
                break;

            case ArrowDrawable arrow:
                arrow.Offset = state.Offset;
                arrow.EndPoint = state.EndPoint;
                break;
        }
    }

    public readonly struct ShapeState
    {
        public System.Numerics.Vector2 Offset { get; init; }
        public Size Size { get; init; }
        public System.Numerics.Vector2 EndPoint { get; init; }

        public ShapeState(IDrawable shape)
        {
            switch (shape)
            {
                case RectangleDrawable rect:
                    Offset = rect.Offset;
                    Size = rect.Size;
                    EndPoint = default;
                    break;

                case EllipseDrawable ellipse:
                    Offset = ellipse.Offset;
                    Size = ellipse.Size;
                    EndPoint = default;
                    break;

                case LineDrawable line:
                    Offset = line.Offset;
                    Size = default;
                    EndPoint = line.EndPoint;
                    break;

                case ArrowDrawable arrow:
                    Offset = arrow.Offset;
                    Size = default;
                    EndPoint = arrow.EndPoint;
                    break;

                default:
                    Offset = default;
                    Size = default;
                    EndPoint = default;
                    break;
            }
        }
    }
}
