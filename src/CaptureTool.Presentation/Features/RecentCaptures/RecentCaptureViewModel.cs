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

    public RecentCaptureViewModel(string temporaryFilePath)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = CaptureFileTypeDetector.DetectFileType(temporaryFilePath);
    }
}
