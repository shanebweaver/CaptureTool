using CaptureTool.Common.Commands;
using System.Collections.Generic;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISettingsPageViewModel
{
    IAsyncCommand ChangeScreenshotsFolderCommand { get; }
    ICommand OpenScreenshotsFolderCommand { get; }
    IAsyncCommand ChangeVideosFolderCommand { get; }
    ICommand OpenVideosFolderCommand { get; }
    ICommand RestartAppCommand { get; }
    ICommand GoBackCommand { get; }
    IAsyncCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    IAsyncCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    IAsyncCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    IAsyncCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    IAsyncCommand<int> UpdateAppLanguageCommand { get; }
    ICommand UpdateAppThemeCommand { get; }
    ICommand OpenTemporaryFilesFolderCommand { get; }
    ICommand ClearTemporaryFilesCommand { get; }
    IAsyncCommand RestoreDefaultSettingsCommand { get; }
    
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
