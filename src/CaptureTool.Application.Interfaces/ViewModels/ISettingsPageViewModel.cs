using CaptureTool.Common.Commands;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISettingsPageViewModel
{
    AsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    RelayCommand OpenScreenshotsFolderCommand { get; }
    AsyncRelayCommand ChangeVideosFolderCommand { get; }
    RelayCommand OpenVideosFolderCommand { get; }
    RelayCommand RestartAppCommand { get; }
    RelayCommand GoBackCommand { get; }
    AsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    AsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    AsyncRelayCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    AsyncRelayCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    AsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    RelayCommand<int> UpdateAppThemeCommand { get; }
    RelayCommand OpenTemporaryFilesFolderCommand { get; }
    RelayCommand ClearTemporaryFilesCommand { get; }
    AsyncRelayCommand RestoreDefaultSettingsCommand { get; }
    
    ObservableCollection<IAppLanguageViewModel> AppLanguages { get; }
    int SelectedAppLanguageIndex { get; }
    bool ShowAppLanguageRestartMessage { get; }
    ObservableCollection<IAppThemeViewModel> AppThemes { get; }
    int SelectedAppThemeIndex { get; }
    bool ShowAppThemeRestartMessage { get; }
    bool IsVideoCaptureFeatureEnabled { get; }
    bool ImageCaptureAutoCopy { get; }
    bool ImageCaptureAutoSave { get; }
    bool VideoCaptureAutoCopy { get; }
    bool VideoCaptureAutoSave { get; }
    string ScreenshotsFolderPath { get; }
    string VideosFolderPath { get; }
    string TemporaryFilesFolderPath { get; }
    
    Task LoadAsync(CancellationToken cancellationToken);
}
