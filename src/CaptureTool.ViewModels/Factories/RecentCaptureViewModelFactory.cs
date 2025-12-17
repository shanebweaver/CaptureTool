using CaptureTool.Core.Interfaces;
using CaptureTool.Services.Interfaces;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class RecentCaptureViewModelFactory : IFactoryServiceWithArgs<RecentCaptureViewModel, string>
{
    private readonly IFileTypeDetector _fileTypeDetector;

    public RecentCaptureViewModelFactory(IFileTypeDetector fileTypeDetector)
    {
        _fileTypeDetector = fileTypeDetector;
    }

    public RecentCaptureViewModel Create(string args)
    {
        return new RecentCaptureViewModel(args, _fileTypeDetector);
    }
}