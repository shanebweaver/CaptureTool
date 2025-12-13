namespace CaptureTool.Core.Interfaces.Actions.VideoEdit;

public interface IVideoEditActions
{
    Task SaveAsync(string videoPath, CancellationToken ct);
    Task CopyAsync(string videoPath, CancellationToken ct);
}
