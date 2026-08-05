using System.Drawing;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class SetCropCommand : IImageEditCommand, IResolutionAwareImageEditCommand
{
    private Rectangle _oldCropRect;
    private Rectangle _newCropRect;

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

    void IResolutionAwareImageEditCommand.Rebase(ImageEditSession session, double scaleX, double scaleY)
    {
        _oldCropRect = ImageEditSession.ScaleRectangle(_oldCropRect, scaleX, scaleY);
        _newCropRect = ImageEditSession.ScaleRectangle(_newCropRect, scaleX, scaleY);
    }
}
