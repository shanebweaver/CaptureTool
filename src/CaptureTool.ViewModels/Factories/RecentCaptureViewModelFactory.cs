using CaptureTool.Services.Interfaces;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class RecentCaptureViewModelFactory : IFactoryServiceWithArgs<RecentCaptureViewModel, string>
{
    public RecentCaptureViewModel Create(string args)
    {
        return new RecentCaptureViewModel(args);
    }
}