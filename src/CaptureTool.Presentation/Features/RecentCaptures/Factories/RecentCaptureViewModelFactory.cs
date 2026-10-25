using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.RecentCaptures.Factories;

public sealed partial class RecentCaptureViewModelFactory(ILocalizationService localization) : IFactoryServiceWithArgs<RecentCaptureViewModel, string>
{
    public RecentCaptureViewModel Create(string args)
    {
        return new RecentCaptureViewModel(args, localization);
    }
}
