using CaptureTool.Domain.Edit.Interfaces.Drawable;
using System.Collections.ObjectModel;

namespace CaptureTool.Domain.Edit.Interfaces.Operations;

public sealed class DeleteShapeOperation : CanvasOperation
{
    private readonly ObservableCollection<IDrawable> _drawables;
    private readonly IDrawable _shape;
    private readonly int _index;
    private readonly Action? _invalidateCallback;

    public DeleteShapeOperation(ObservableCollection<IDrawable> drawables, IDrawable shape, int index, Action? invalidateCallback = null)
    {
        _drawables = drawables;
        _shape = shape;
        _index = index;
        _invalidateCallback = invalidateCallback;
    }

    public override void Undo()
    {
        _drawables.Insert(_index, _shape);
        _invalidateCallback?.Invoke();
    }

    public override void Redo()
    {
        _drawables.Remove(_shape);
        _invalidateCallback?.Invoke();
    }
}
