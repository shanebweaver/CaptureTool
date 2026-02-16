using CaptureTool.Domain.Edit.Interfaces.Drawable;
using System.Collections.ObjectModel;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed class AddShapeOperation : CanvasOperation
{
    private readonly ObservableCollection<IDrawable> _drawables;
    private readonly IDrawable _shape;
    private readonly Action? _invalidateCallback;

    public AddShapeOperation(ObservableCollection<IDrawable> drawables, IDrawable shape, Action? invalidateCallback = null)
    {
        _drawables = drawables;
        _shape = shape;
        _invalidateCallback = invalidateCallback;
    }

    public override void Undo()
    {
        _drawables.Remove(_shape);
        _invalidateCallback?.Invoke();
    }

    public override void Redo()
    {
        _drawables.Add(_shape);
        _invalidateCallback?.Invoke();
    }
}
