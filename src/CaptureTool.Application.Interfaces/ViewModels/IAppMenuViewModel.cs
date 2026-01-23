using CaptureTool.Application.Interfaces.Commands;
using System.Collections.Generic;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAppMenuViewModel
{
    event EventHandler? RecentCapturesUpdated;
    
    ICommand NewImageCaptureCommand { get; }
    IAsyncCommand OpenFileCommand { get; }
    ICommand NavigateToSettingsCommand { get; }
    ICommand ShowAboutAppCommand { get; }
    ICommand ShowAddOnsCommand { get; }
    ICommand ExitApplicationCommand { get; }
    ICommand RefreshRecentCapturesCommand { get; }
    ICommand OpenRecentCaptureCommand { get; }
    bool ShowAddOnsOption { get; }
    IReadOnlyList<IRecentCaptureViewModel> RecentCaptures { get; set; }
    
    void Load();
    void RefreshRecentCaptures();
}
