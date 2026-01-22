using CaptureTool.Common;
using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class RecentCaptureViewModel : ViewModelBase, IRecentCaptureViewModel
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

    public RecentCaptureViewModel(string temporaryFilePath, IFileTypeDetector fileTypeDetector)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = fileTypeDetector.DetectFileType(temporaryFilePath);
    }
}
