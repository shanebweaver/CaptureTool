using CaptureTool.Application.Interfaces;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations;

public sealed class FileTypeDetector : IFileTypeDetector
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mov", ".wmv"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac"
    };

    public CaptureFileType DetectFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath);

        if (ImageExtensions.Contains(ext)) return CaptureFileType.Image;
        if (VideoExtensions.Contains(ext)) return CaptureFileType.Video;
        if (AudioExtensions.Contains(ext)) return CaptureFileType.Audio;

        return CaptureFileType.Unknown;
    }

    public bool IsImageFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Image;

    public bool IsVideoFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Video;

    public bool IsAudioFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Audio;
}
