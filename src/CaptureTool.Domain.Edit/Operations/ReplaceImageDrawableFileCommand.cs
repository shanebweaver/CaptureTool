using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Domain.Edit.Operations;

public sealed class ReplaceImageDrawableFileCommand : IImageEditCommand
{
    private readonly int _drawableIndex;
    private readonly ImageFile _originalFile;
    private readonly ImageFile _replacementFile;

    public ReplaceImageDrawableFileCommand(
        int drawableIndex,
        ImageFile originalFile,
        ImageFile replacementFile)
    {
        _drawableIndex = drawableIndex;
        _originalFile = originalFile;
        _replacementFile = replacementFile;
    }

    public void Apply(ImageEditSession session)
    {
        GetImageDrawable(session).File = _replacementFile;
    }

    public void Revert(ImageEditSession session)
    {
        GetImageDrawable(session).File = _originalFile;
    }

    private ImageDrawable GetImageDrawable(ImageEditSession session)
    {
        return session.GetDrawableAt(_drawableIndex) as ImageDrawable
            ?? throw new InvalidOperationException("The target drawable is not an image.");
    }
}
