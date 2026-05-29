using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.ViewModels;

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

    public RecentCaptureViewModel(string temporaryFilePath, IFileTypeDetector fileTypeDetector)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = fileTypeDetector.DetectFileType(temporaryFilePath);
    }
}
