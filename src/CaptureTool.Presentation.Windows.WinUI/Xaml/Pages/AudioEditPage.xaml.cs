using CaptureTool.Application.Implementations.ViewModels;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class AudioEditPage : AudioEditPageBase
{
    public AudioEditPage()
    {
        InitializeComponent();
        AudioPlayer.SetMediaPlayer(new MediaPlayer());
        AudioPlayer.Loaded += AudioPlayer_Loaded;
        AudioPlayer.Unloaded += AudioPlayer_Unloaded;
    }

    private void AudioPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void AudioPlayer_Loaded(object sender, RoutedEventArgs e)
    {
        TryInitializeAudio();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioEditPageViewModel.AudioPath) ||
            e.PropertyName == nameof(AudioEditPageViewModel.IsAudioReady))
        {
            TryInitializeAudio();
        }
    }

    private bool TryInitializeAudio()
    {
        if (!string.IsNullOrEmpty(ViewModel.AudioPath) && ViewModel.IsAudioReady)
        {
            _ = InitializeAudioAsync(ViewModel.AudioPath);
            return true;
        }

        return false;
    }

    private async Task InitializeAudioAsync(string filePath)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            var mediaSource = MediaSource.CreateFromStorageFile(file);
            AudioPlayer.MediaPlayer.Source = mediaSource;
        }
        catch (Exception)
        {
            // Audio not ready yet or file doesn't exist
        }
    }
}
