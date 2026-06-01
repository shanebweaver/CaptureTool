using CaptureTool.Infrastructure.Abstractions.Media;
using System.Runtime.InteropServices;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace CaptureTool.Infrastructure.Windows.Media;

public sealed class WindowsVideoFileTrimmer : IVideoFileTrimmer
{
    public async Task TrimAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan trimStart,
        TimeSpan trimEnd,
        CancellationToken cancellationToken = default)
    {
        StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);
        MediaClip clip = await MediaClip.CreateFromFileAsync(sourceFile);
        TimeSpan normalizedTrimStart = Max(TimeSpan.Zero, trimStart);
        TimeSpan normalizedTrimEnd = Min(clip.OriginalDuration, trimEnd);

        if (normalizedTrimEnd <= normalizedTrimStart)
        {
            throw new InvalidOperationException("Cannot trim video to an empty duration.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        clip.TrimTimeFromStart = normalizedTrimStart;
        clip.TrimTimeFromEnd = clip.OriginalDuration - normalizedTrimEnd;

        var composition = new MediaComposition();
        composition.Clips.Add(clip);

        string renderDestinationPath = GetRenderDestinationPath(sourcePath, destinationPath);
        EnsureOutputFileExists(renderDestinationPath);
        StorageFile destinationFile = await StorageFile.GetFileFromPathAsync(renderDestinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        TranscodeFailureReason result = await RenderWithFallbackAsync(
            composition,
            destinationFile,
            renderDestinationPath,
            cancellationToken);

        if (result != TranscodeFailureReason.None)
        {
            throw new InvalidOperationException($"Failed to trim video. Transcode failure: {result}.");
        }

        if (!string.Equals(renderDestinationPath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(renderDestinationPath, destinationPath, true);
            File.Delete(renderDestinationPath);
        }
    }

    private static async Task<TranscodeFailureReason> RenderWithFallbackAsync(
        MediaComposition composition,
        StorageFile destinationFile,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await composition.RenderToFileAsync(
                destinationFile,
                MediaTrimmingPreference.Precise).AsTask(cancellationToken);
        }
        catch (COMException)
        {
            ResetOutputFile(destinationPath);
            destinationFile = await StorageFile.GetFileFromPathAsync(destinationPath);
            return await composition.RenderToFileAsync(
                destinationFile,
                MediaTrimmingPreference.Fast).AsTask(cancellationToken);
        }
    }

    private static void ResetOutputFile(string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        EnsureOutputFileExists(destinationPath);
    }

    private static string GetRenderDestinationPath(string sourcePath, string destinationPath)
    {
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
        {
            return destinationPath;
        }

        string directoryPath = Path.GetDirectoryName(destinationPath) ?? Path.GetTempPath();
        string fileName = $"{Path.GetFileNameWithoutExtension(destinationPath)}-{Guid.NewGuid():N}.mp4";
        return Path.Combine(directoryPath, fileName);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return left >= right ? left : right;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return left <= right ? left : right;
    }

    private static void EnsureOutputFileExists(string destinationPath)
    {
        string? directoryPath = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (!File.Exists(destinationPath))
        {
            using FileStream stream = File.Create(destinationPath);
        }
    }
}
