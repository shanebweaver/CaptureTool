using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Presentation.Features.Media;

internal static class MediaWorkingCopy
{
    public static void RestoreIfMissing(FileReference file, string? sourcePath,
        IFileSystem? files, IScratchArtifactStore? scratch)
    {
        if (files == null || scratch == null || string.IsNullOrWhiteSpace(sourcePath) ||
            files.FileExists(file.FilePath) || !files.FileExists(sourcePath)) return;

        // Back navigation reuses the file reference after the previous editor released its copy.
        string workingPath = scratch.CreateLeasedArtifactPath("restored-media-working-copy", Path.GetExtension(sourcePath));
        try
        {
            files.CopyFile(sourcePath, workingPath, true);
            file.FilePath = workingPath;
        }
        catch
        {
            scratch.DeleteArtifact(workingPath);
            throw;
        }
    }
}
