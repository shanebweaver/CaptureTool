namespace CaptureTool.Domain.Capture;

public static class CaptureFileTypeDetector
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
        ".wav"
    };

    public static CaptureFileType DetectFileType(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        if (ImageExtensions.Contains(extension))
        {
            return CaptureFileType.Image;
        }

        if (VideoExtensions.Contains(extension))
        {
            return CaptureFileType.Video;
        }

        return AudioExtensions.Contains(extension)
            ? CaptureFileType.Audio
            : CaptureFileType.Unknown;
    }

    public static bool IsImageFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Image;

    public static bool IsVideoFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Video;

    public static bool IsAudioFile(string filePath)
        => DetectFileType(filePath) == CaptureFileType.Audio;
}
