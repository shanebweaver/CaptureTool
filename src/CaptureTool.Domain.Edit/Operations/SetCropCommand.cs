using System.Drawing;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class SetCropCommand : IImageEditCommand
{
    private readonly Rectangle _oldCropRect;
    private readonly Rectangle _newCropRect;

    public SetCropCommand(Rectangle oldCropRect, Rectangle newCropRect)
    {
        _oldCropRect = oldCropRect;
        _newCropRect = newCropRect;
    }

    public void Apply(ImageEditSession session)
    {
        session.SetCropRect(_newCropRect);
    }

    public void Revert(ImageEditSession session)
    {
        session.SetCropRect(_oldCropRect);
    }
}
