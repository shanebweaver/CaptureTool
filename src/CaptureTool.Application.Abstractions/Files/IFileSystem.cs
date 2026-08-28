namespace CaptureTool.Application.Abstractions.Files;

public interface IFileSystem
{
    bool FileExists(string filePath);
    bool DirectoryExists(string folderPath);
    IEnumerable<string> EnumerateFiles(string folderPath, string searchPattern);
    IEnumerable<string> EnumerateFilesRecursively(string folderPath, string searchPattern);
    IEnumerable<string> EnumerateFileSystemEntries(string folderPath);
    long GetFileLength(string filePath);
    DateTime GetLastWriteTimeUtc(string filePath);
    void SetLastWriteTimeUtc(string filePath, DateTime lastWriteTimeUtc);
    void CreateDirectory(string folderPath);
    void CreateEmptyFile(string filePath);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
    void DeleteFile(string filePath);
    void DeleteDirectory(string folderPath, bool recursive);
    Task WriteAllTextAsync(string filePath, string contents, CancellationToken cancellationToken = default);
}
