using CaptureTool.Common;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.ViewModels;

public sealed partial class RecentCaptureViewModel : ViewModelBase
{
    public string FilePath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string FileName
    {
        get => field;
        private set => Set(ref field, value);
    }

    public CaptureFileType CaptureFileType
    {
        get => field;
        private set => Set(ref field, value);
    }

    public RecentCaptureViewModel(string temporaryFilePath)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = DetectFileType(temporaryFilePath);
    }

    private static CaptureFileType DetectFileType(string filePath)
    {
        return Path.GetExtension(filePath) switch
        {
            ".png" => CaptureFileType.Image,
            ".mp4" => CaptureFileType.Video,
            _ => CaptureFileType.Unknown,
        };
    }
}
