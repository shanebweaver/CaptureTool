namespace CaptureTool.Infrastructure.Abstractions.Media;

public interface IVideoFileTrimmer
{
    Task TrimAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan trimStart,
        TimeSpan trimEnd,
        CancellationToken cancellationToken = default);
}
