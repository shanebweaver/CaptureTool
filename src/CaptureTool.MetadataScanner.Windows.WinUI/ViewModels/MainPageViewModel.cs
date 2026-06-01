using CaptureTool.MetadataScanner.Windows.WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Media.Core;

namespace CaptureTool.MetadataScanner.Windows.WinUI.ViewModels;

public sealed partial class MainPageViewModel(IMediaFilePicker mediaFilePicker) : ObservableObject
{
    private MediaSource? mediaSource;
    private string selectedFileName = "No media file selected";

    public MediaSource? MediaSource
    {
        get => mediaSource;
        set => SetProperty(ref mediaSource, value);
    }

    public string SelectedFileName
    {
        get => selectedFileName;
        set => SetProperty(ref selectedFileName, value);
    }

    [RelayCommand]
    private async Task OpenMediaFileAsync()
    {
        var mediaFile = await mediaFilePicker.PickMediaFileAsync();
        if (mediaFile is null)
        {
            return;
        }

        MediaSource = MediaSource.CreateFromStorageFile(mediaFile);
        SelectedFileName = mediaFile.Name;
    }
}
