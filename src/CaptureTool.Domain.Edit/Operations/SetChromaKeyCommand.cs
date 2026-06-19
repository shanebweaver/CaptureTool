namespace CaptureTool.Domain.Edit.Operations;

public sealed class SetChromaKeyCommand : IImageEditCommand
{
    private readonly ChromaKeySettings _oldSettings;
    private readonly ChromaKeySettings _newSettings;

    public SetChromaKeyCommand(ChromaKeySettings oldSettings, ChromaKeySettings newSettings)
    {
        _oldSettings = oldSettings;
        _newSettings = newSettings;
    }

    public void Apply(ImageEditSession session)
    {
        session.SetChromaKeySettings(_newSettings);
    }

    public void Revert(ImageEditSession session)
    {
        session.SetChromaKeySettings(_oldSettings);
    }
}
