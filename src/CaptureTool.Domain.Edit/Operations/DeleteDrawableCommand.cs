using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class DeleteDrawableCommand : IImageEditCommand, IResolutionAwareImageEditCommand
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

    void IResolutionAwareImageEditCommand.Rebase(ImageEditSession session, double scaleX, double scaleY)
    {
        if (_drawable is not null &&
            !session.Drawables.Any(drawable => ReferenceEquals(drawable, _drawable)))
        {
            ImageEditSession.ScaleDrawable(_drawable, scaleX, scaleY);
        }
    }
}
