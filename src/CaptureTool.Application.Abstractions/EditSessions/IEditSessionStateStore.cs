namespace CaptureTool.Application.Abstractions.EditSessions;

public interface IEditSessionStateStore
{
    Task SaveVideoTrimStateAsync(string videoFilePath, VideoTrimState state, CancellationToken cancellationToken = default);
    Task<VideoTrimState?> TryReadVideoTrimStateAsync(string videoFilePath, CancellationToken cancellationToken = default);
}
