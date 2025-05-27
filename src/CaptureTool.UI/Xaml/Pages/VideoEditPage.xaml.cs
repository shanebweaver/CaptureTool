using CaptureTool.ViewModels;
using System;
using System.ComponentModel;
using Windows.Media.Core;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class VideoEditPage : VideoEditPageBase
{
    public VideoEditPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VideoEditPageViewModel.VideoFile):
                UpdateMediaPlayerSource();
                break;
        }
    }

    private void UpdateMediaPlayerSource()
    {
        if (ViewModel.VideoFile != null)
        {
            Uri pathUri = new(ViewModel.VideoFile.Path);
            VideoMediaPlayer.Source = MediaSource.CreateFromUri(pathUri);
        }
        else
        {
            VideoMediaPlayer.Source = null;
        }
    }
}
