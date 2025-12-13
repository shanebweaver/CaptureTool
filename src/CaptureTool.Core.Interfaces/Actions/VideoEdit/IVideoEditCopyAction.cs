namespace CaptureTool.Core.Interfaces.Actions.VideoEdit;

public interface IVideoEditCopyAction
{
    Task CopyAsync(string videoPath, CancellationToken ct);
}
