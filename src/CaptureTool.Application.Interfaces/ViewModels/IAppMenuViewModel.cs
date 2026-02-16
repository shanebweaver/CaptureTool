using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAppMenuViewModel : IViewModel
{
    event EventHandler? RecentCapturesUpdated;

    IAppCommand NewImageCaptureCommand { get; }
    IAppCommand NewVideoCaptureCommand { get; }
    IAsyncAppCommand OpenFileCommand { get; }
    IAppCommand NavigateToSettingsCommand { get; }
    IAppCommand ShowAboutAppCommand { get; }
    IAppCommand ShowAddOnsCommand { get; }
    IAppCommand ExitApplicationCommand { get; }
    IAppCommand RefreshRecentCapturesCommand { get; }
    IAppCommand<IRecentCaptureViewModel> OpenRecentCaptureCommand { get; }
    bool ShowAddOnsOption { get; }
    bool IsVideoCaptureEnabled { get; }
    IReadOnlyList<IRecentCaptureViewModel> RecentCaptures { get; set; }

    void Load();
    void RefreshRecentCaptures();
}
