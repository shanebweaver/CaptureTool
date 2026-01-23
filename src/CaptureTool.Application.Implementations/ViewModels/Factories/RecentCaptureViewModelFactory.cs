using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

public sealed partial class RecentCaptureViewModelFactory : IFactoryServiceWithArgs<IRecentCaptureViewModel, string>
{
    private readonly IFileTypeDetector _fileTypeDetector;

    public RecentCaptureViewModelFactory(IFileTypeDetector fileTypeDetector)
    {
        _fileTypeDetector = fileTypeDetector;
    }

    public IRecentCaptureViewModel Create(string args)
    {
        return new RecentCaptureViewModel(args, _fileTypeDetector);
    }
}