namespace CaptureTool.Domain.Edit.Operations;

internal interface IResolutionAwareImageEditCommand
{
    void Rebase(ImageEditSession session, double scaleX, double scaleY);
}
