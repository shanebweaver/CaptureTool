using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.RecentCaptures.Factories;

public sealed partial class RecentCaptureViewModelFactory : IFactoryServiceWithArgs<RecentCaptureViewModel, string>
{
    public RecentCaptureViewModel Create(string args)
    {
        return new RecentCaptureViewModel(args);
    }
}
