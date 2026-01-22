using CaptureTool.Common.Commands;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAppMenuViewModel
{
    event EventHandler? RecentCapturesUpdated;
    
    RelayCommand NewImageCaptureCommand { get; }
    AsyncRelayCommand OpenFileCommand { get; }
    RelayCommand NavigateToSettingsCommand { get; }
    RelayCommand ShowAboutAppCommand { get; }
    RelayCommand ShowAddOnsCommand { get; }
    RelayCommand ExitApplicationCommand { get; }
    RelayCommand RefreshRecentCapturesCommand { get; }
    RelayCommand<IRecentCaptureViewModel> OpenRecentCaptureCommand { get; }
    bool ShowAddOnsOption { get; }
    ObservableCollection<IRecentCaptureViewModel> RecentCaptures { get; set; }
    
    void Load();
    void RefreshRecentCaptures();
}
