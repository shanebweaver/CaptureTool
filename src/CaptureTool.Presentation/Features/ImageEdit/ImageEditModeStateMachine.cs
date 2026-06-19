namespace CaptureTool.Presentation.Features.ImageEdit;

internal sealed class ImageEditModeStateMachine
{
    public ImageEditMode ActiveMode { get; private set; } = ImageEditMode.Normal;

    public ImageEditMode Activate(ImageEditMode mode)
    {
        ActiveMode = mode;
        return ActiveMode;
    }

    public ImageEditMode Deactivate(ImageEditMode mode)
    {
        if (ActiveMode == mode)
        {
            ActiveMode = ImageEditMode.Normal;
        }

        return ActiveMode;
    }

    public ImageEditMode Toggle(ImageEditMode mode)
    {
        return ActiveMode == mode
            ? Deactivate(mode)
            : Activate(mode);
    }

    public ImageEditMode Reset()
    {
        ActiveMode = ImageEditMode.Normal;
        return ActiveMode;
    }
}
