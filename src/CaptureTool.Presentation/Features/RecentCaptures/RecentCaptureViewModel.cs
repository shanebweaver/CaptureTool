using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.RecentCaptures;

public sealed partial class RecentCaptureViewModel : ViewModelBase
{
    public string FilePath
    {
        get;
        private set => Set(ref field, value);
    }

    public string FileName
    {
        get;
        private set => Set(ref field, value);
    }

    public CaptureFileType CaptureFileType
    {
        get;
        private set => Set(ref field, value);
    }

    public string CaptureTypeLabel => CaptureFileType switch
    {
        CaptureFileType.Image => "Image",
        CaptureFileType.Video => "Video",
        CaptureFileType.Audio => "Audio",
        _ => "File"
    };

    public string IconGlyph => CaptureFileType switch
    {
        CaptureFileType.Image => "\uE722",
        CaptureFileType.Video => "\uE714",
        CaptureFileType.Audio => "\uE720",
        _ => "\uE7C3"
    };

    public bool CanLoadThumbnail => CaptureFileType is CaptureFileType.Image or CaptureFileType.Video;

    public RecentCaptureViewModel(string temporaryFilePath)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = CaptureFileTypeDetector.DetectFileType(temporaryFilePath);
    }
}
