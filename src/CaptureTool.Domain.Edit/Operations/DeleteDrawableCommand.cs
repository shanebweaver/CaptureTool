using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class DeleteDrawableCommand : IImageEditCommand
{
    private readonly int _index;
    private IDrawable? _drawable;

    public DeleteDrawableCommand(int index)
    {
        _index = index;
    }

    public void Apply(ImageEditSession session)
    {
        _drawable = session.RemoveDrawableAt(_index);
    }

    public void Revert(ImageEditSession session)
    {
        if (_drawable == null)
        {
            return;
        }

        session.InsertDrawable(_index, _drawable);
    }
}
