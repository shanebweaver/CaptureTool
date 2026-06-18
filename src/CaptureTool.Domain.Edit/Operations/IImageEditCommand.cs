namespace CaptureTool.Domain.Edit.Operations;

public interface IImageEditCommand
{
    void Apply(ImageEditSession session);

    void Revert(ImageEditSession session);
}
