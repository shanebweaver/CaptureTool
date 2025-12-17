using CaptureTool.Core.Interfaces;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Implementations;

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

    public CaptureFileType DetectFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        
        if (ImageExtensions.Contains(ext)) return CaptureFileType.Image;
        if (VideoExtensions.Contains(ext)) return CaptureFileType.Video;
        
        return CaptureFileType.Unknown;
    }
    
    public bool IsImageFile(string filePath) 
        => DetectFileType(filePath) == CaptureFileType.Image;
        
    public bool IsVideoFile(string filePath) 
        => DetectFileType(filePath) == CaptureFileType.Video;
}
