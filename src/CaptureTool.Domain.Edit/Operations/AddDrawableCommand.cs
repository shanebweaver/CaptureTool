using CaptureTool.Domain.Edit.Drawable;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class AddDrawableCommand : IImageEditCommand
{
    private readonly IDrawable _drawable;
    private int? _index;

    public AddDrawableCommand(IDrawable drawable)
    {
        _drawable = drawable;
    }

    public void Apply(ImageEditSession session)
    {
        if (_index is { } index)
        {
            session.InsertDrawable(index, _drawable);
            return;
        }

        session.AddDrawable(_drawable);
        _index = session.Drawables.Count - 1;
    }

    public void Revert(ImageEditSession session)
    {
        if (_index is { } index && index < session.Drawables.Count && ReferenceEquals(session.Drawables[index], _drawable))
        {
            session.RemoveDrawableAt(index);
            return;
        }

        session.RemoveDrawable(_drawable);
    }
}
