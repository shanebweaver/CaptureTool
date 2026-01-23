using System.Collections.Generic;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISettingsPageViewModel
{
    ICommand ChangeScreenshotsFolderCommand { get; }
    ICommand OpenScreenshotsFolderCommand { get; }
    ICommand ChangeVideosFolderCommand { get; }
    ICommand OpenVideosFolderCommand { get; }
    ICommand RestartAppCommand { get; }
    ICommand GoBackCommand { get; }
    ICommand UpdateImageCaptureAutoCopyCommand { get; }
    ICommand UpdateImageCaptureAutoSaveCommand { get; }
    ICommand UpdateVideoCaptureAutoCopyCommand { get; }
    ICommand UpdateVideoCaptureAutoSaveCommand { get; }
    ICommand UpdateAppLanguageCommand { get; }
    ICommand UpdateAppThemeCommand { get; }
    ICommand OpenTemporaryFilesFolderCommand { get; }
    ICommand ClearTemporaryFilesCommand { get; }
    ICommand RestoreDefaultSettingsCommand { get; }
    
    IReadOnlyList<IAppLanguageViewModel> AppLanguages { get; }
    int SelectedAppLanguageIndex { get; }
    bool ShowAppLanguageRestartMessage { get; }
    IReadOnlyList<IAppThemeViewModel> AppThemes { get; }
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
