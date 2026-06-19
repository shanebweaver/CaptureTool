using CaptureTool.Application.Abstractions.EditSessions;
using System.Globalization;

namespace CaptureTool.Infrastructure.EditSessions;

public sealed class FileEditSessionStateStore : IEditSessionStateStore
{
    private const string VideoTrimStateExtension = ".ctedit.json";

    public async Task SaveVideoTrimStateAsync(string videoFilePath, VideoTrimState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
        {
            return;
        }

        string statePath = GetVideoTrimStatePath(videoFilePath);
        string text = string.Join(
            Environment.NewLine,
            state.DurationSeconds.ToString("R", CultureInfo.InvariantCulture),
            state.TrimStartSeconds.ToString("R", CultureInfo.InvariantCulture),
            state.TrimEndSeconds.ToString("R", CultureInfo.InvariantCulture));
        await File.WriteAllTextAsync(statePath, text, cancellationToken);
    }

    public async Task<VideoTrimState?> TryReadVideoTrimStateAsync(string videoFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
        {
            return null;
        }

        string statePath = GetVideoTrimStatePath(videoFilePath);
        if (!File.Exists(statePath))
        {
            return null;
        }

        string[] lines = await File.ReadAllLinesAsync(statePath, cancellationToken);
        if (lines.Length < 3 ||
            !double.TryParse(lines[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double durationSeconds) ||
            !double.TryParse(lines[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double trimStartSeconds) ||
            !double.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double trimEndSeconds))
        {
            return null;
        }

        return new VideoTrimState(durationSeconds, trimStartSeconds, trimEndSeconds);
    }

    private static string GetVideoTrimStatePath(string videoFilePath) => $"{videoFilePath}{VideoTrimStateExtension}";
}
