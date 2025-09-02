using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class VideoEditPage : VideoEditPageBase
{
    public VideoEditPage()
    {
        InitializeComponent();
        VideoPlayer.SetMediaPlayer(new MediaPlayer());
        VideoPlayer.Loaded += VideoPlayer_Loaded;
        VideoPlayer.Unloaded += VideoPlayer_Unloaded;
    }

    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
    {
        TryInitializeVideo();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoEditPageViewModel.VideoPath))
        {
            TryInitializeVideo();
        }
    }

    private bool TryInitializeVideo()
    {
        if (!string.IsNullOrEmpty(ViewModel.VideoPath))
        {
            InitializeVideoAsync(ViewModel.VideoPath);
            return true;
        }

        return false;
    }

    private async void InitializeVideoAsync(string filePath)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        var mediaSource = MediaSource.CreateFromStorageFile(file);
        VideoPlayer.MediaPlayer.Source = mediaSource;
    }
}
