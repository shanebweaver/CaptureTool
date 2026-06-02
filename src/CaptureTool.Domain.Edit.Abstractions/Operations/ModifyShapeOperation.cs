using CaptureTool.Domain.Edit.Abstractions.Drawable;

namespace CaptureTool.Domain.Edit.Abstractions.Operations;

public sealed partial class ModifyShapeOperation : CanvasOperation
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
                rect.StrokeColor = state.StrokeColor;
                rect.FillColor = state.FillColor;
                rect.StrokeWidth = state.StrokeWidth;
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = state.Offset;
                ellipse.Size = state.Size;
                ellipse.StrokeColor = state.StrokeColor;
                ellipse.FillColor = state.FillColor;
                ellipse.StrokeWidth = state.StrokeWidth;
                break;

            case LineDrawable line:
                line.Offset = state.Offset;
                line.EndPoint = state.EndPoint;
                line.StrokeColor = state.StrokeColor;
                line.StrokeWidth = state.StrokeWidth;
                break;

            case ArrowDrawable arrow:
                arrow.Offset = state.Offset;
                arrow.EndPoint = state.EndPoint;
                arrow.StrokeColor = state.StrokeColor;
                arrow.StrokeWidth = state.StrokeWidth;
                break;

            case TextDrawable text:
                text.Offset = state.Offset;
                text.Size = state.Size;
                text.Color = state.StrokeColor;
                text.Text = state.Text;
                text.FontFamily = state.FontFamily;
                text.FontSize = state.FontSize;
                break;
        }
    }
}
