using CaptureTool.Application.Abstractions.Files;

namespace CaptureTool.Application.Tests;

internal sealed class TestFileSystem : IFileSystem
{
    public static TestFileSystem Instance { get; } = new();

    private TestFileSystem()
    {
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public bool DirectoryExists(string folderPath) => Directory.Exists(folderPath);

    public IEnumerable<string> EnumerateFiles(string folderPath, string searchPattern) =>
        Directory.EnumerateFiles(folderPath, searchPattern);

    public IEnumerable<string> EnumerateFileSystemEntries(string folderPath) =>
        Directory.EnumerateFileSystemEntries(folderPath);

    public DateTime GetLastWriteTimeUtc(string filePath) => File.GetLastWriteTimeUtc(filePath);

    public void SetLastWriteTimeUtc(string filePath, DateTime lastWriteTimeUtc) =>
        File.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);

    public void CreateDirectory(string folderPath) => Directory.CreateDirectory(folderPath);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Copy(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string filePath) => File.Delete(filePath);

    public void DeleteDirectory(string folderPath, bool recursive) =>
        Directory.Delete(folderPath, recursive);

    public Task WriteAllTextAsync(string filePath, string contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(filePath, contents, cancellationToken);
}
