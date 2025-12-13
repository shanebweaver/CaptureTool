namespace CaptureTool.Core.Interfaces.Actions.VideoEdit;

public interface IVideoEditSaveAction
{
    Task SaveAsync(string videoPath, CancellationToken ct);
}
