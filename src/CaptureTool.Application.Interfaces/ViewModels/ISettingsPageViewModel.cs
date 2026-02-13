using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface ISettingsPageViewModel : IViewModel
{
    IAsyncAppCommand ChangeScreenshotsFolderCommand { get; }
    IAppCommand OpenScreenshotsFolderCommand { get; }
    IAsyncAppCommand ChangeVideosFolderCommand { get; }
    IAppCommand OpenVideosFolderCommand { get; }
    IAppCommand RestartAppCommand { get; }
    IAppCommand GoBackCommand { get; }
    IAsyncAppCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    IAsyncAppCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    IAsyncAppCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    IAsyncAppCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    IAsyncAppCommand<bool> UpdateVideoCaptureDefaultLocalAudioCommand { get; }
    IAsyncAppCommand<bool> UpdateVideoMetadataAutoSaveCommand { get; }
    IAsyncAppCommand<int> UpdateAppLanguageCommand { get; }
    IAppCommand<int> UpdateAppThemeCommand { get; }
    IAppCommand OpenTemporaryFilesFolderCommand { get; }
    IAppCommand ClearTemporaryFilesCommand { get; }
    IAsyncAppCommand RestoreDefaultSettingsCommand { get; }

    ObservableCollection<IAppLanguageViewModel> AppLanguages { get; }
    int SelectedAppLanguageIndex { get; }
    bool ShowAppLanguageRestartMessage { get; }
    ObservableCollection<IAppThemeViewModel> AppThemes { get; }
    int SelectedAppThemeIndex { get; }
    bool ShowAppThemeRestartMessage { get; }
    bool IsVideoCaptureFeatureEnabled { get; }
    bool IsVideoMetadataFeatureEnabled { get; }
    bool ImageCaptureAutoCopy { get; }
    bool ImageCaptureAutoSave { get; }
    bool VideoCaptureAutoCopy { get; }
    bool VideoCaptureAutoSave { get; }
    bool VideoCaptureDefaultLocalAudio { get; }
    bool VideoMetadataAutoSave { get; }
    string ScreenshotsFolderPath { get; }
    string VideosFolderPath { get; }
    string TemporaryFilesFolderPath { get; }

    Task LoadAsync(CancellationToken cancellationToken);
}
