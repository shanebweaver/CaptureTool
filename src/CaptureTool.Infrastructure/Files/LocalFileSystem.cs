using CaptureTool.Application.Abstractions.Files;

namespace CaptureTool.Infrastructure.Files;

public sealed class LocalFileSystem : IFileSystem
{
    public bool FileExists(string filePath) => File.Exists(filePath);

    public bool DirectoryExists(string folderPath) => Directory.Exists(folderPath);

    public IEnumerable<string> EnumerateFiles(string folderPath, string searchPattern) =>
        Directory.EnumerateFiles(folderPath, searchPattern);

    public IEnumerable<string> EnumerateFilesRecursively(string folderPath, string searchPattern) =>
        Directory.EnumerateFiles(folderPath, searchPattern, SearchOption.AllDirectories);

    public IEnumerable<string> EnumerateFileSystemEntries(string folderPath) =>
        Directory.EnumerateFileSystemEntries(folderPath);

    public long GetFileLength(string filePath) => new FileInfo(filePath).Length;

    public DateTime GetLastWriteTimeUtc(string filePath) => File.GetLastWriteTimeUtc(filePath);

    public void SetLastWriteTimeUtc(string filePath, DateTime lastWriteTimeUtc) =>
        File.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);

    public void CreateDirectory(string folderPath) => Directory.CreateDirectory(folderPath);

    public void CreateEmptyFile(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Copy(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string filePath) => File.Delete(filePath);

    public void DeleteDirectory(string folderPath, bool recursive) =>
        Directory.Delete(folderPath, recursive);

    public Task WriteAllTextAsync(string filePath, string contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(filePath, contents, cancellationToken);
}
