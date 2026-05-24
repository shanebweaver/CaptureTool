using CaptureTool.Application.Abstractions;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.ViewModels;

namespace CaptureTool.Presentation.ViewModels;

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

    public RecentCaptureViewModel(string temporaryFilePath, IFileTypeDetector fileTypeDetector)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = fileTypeDetector.DetectFileType(temporaryFilePath);
    }
}
