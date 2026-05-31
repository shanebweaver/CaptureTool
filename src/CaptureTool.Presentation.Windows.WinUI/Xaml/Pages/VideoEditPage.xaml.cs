using CaptureTool.Presentation.Features.VideoEdit;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class VideoEditPage : VideoEditPageBase
{
    private readonly DispatcherTimer _trimPlaybackTimer;
    private MediaPlayer? _mediaPlayer;
    private bool _isUpdatingPlayheadFromMedia;

    public VideoEditPage()
    {
        InitializeComponent();
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
        _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        VideoPlayer.SetMediaPlayer(_mediaPlayer);
        VideoPlayer.Loaded += VideoPlayer_Loaded;
        VideoPlayer.Unloaded += VideoPlayer_Unloaded;

        _trimPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _trimPlaybackTimer.Tick += TrimPlaybackTimer_Tick;
    }

    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _trimPlaybackTimer.Stop();
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }
    }

    private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
    {
        TryInitializeVideo();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoEditPageViewModel.VideoPath) ||
            e.PropertyName == nameof(VideoEditPageViewModel.IsVideoReady))
        {
            TryInitializeVideo();
        }

        if (e.PropertyName == nameof(VideoEditPageViewModel.TrimStartSeconds) ||
            e.PropertyName == nameof(VideoEditPageViewModel.TrimEndSeconds) ||
            e.PropertyName == nameof(VideoEditPageViewModel.PlayheadSeconds))
        {
            SyncMediaPositionToPlayhead();
        }
    }

    private bool TryInitializeVideo()
    {
        if (!string.IsNullOrEmpty(ViewModel.VideoPath) && ViewModel.IsVideoReady)
        {
            _ = InitializeVideoAsync(ViewModel.VideoPath);
            return true;
        }

        return false;
    }

    private async Task InitializeVideoAsync(string filePath)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            var mediaSource = MediaSource.CreateFromStorageFile(file);
            if (_mediaPlayer is not null)
            {
                _mediaPlayer.Source = mediaSource;
            }
        }
        catch (Exception)
        {
            // Video not ready yet or file doesn't exist
        }
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.SetVideoDuration(sender.PlaybackSession.NaturalDuration);
            SyncMediaPositionToPlayhead();
        });
    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(StopTrimPreviewAtEnd);
    }

    private void TrimPreviewPlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            PauseTrimPreview();
            return;
        }

        if (ViewModel.PlayheadSeconds >= ViewModel.TrimEndSeconds)
        {
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
        }

        SyncMediaPositionToPlayhead();
        _mediaPlayer.Play();
        _trimPlaybackTimer.Start();
        UpdateTrimPreviewPlayIcon(true);
    }

    private void TrimPlaybackTimer_Tick(object? sender, object e)
    {
        if (_mediaPlayer is null)
        {
            _trimPlaybackTimer.Stop();
            return;
        }

        double currentSeconds = _mediaPlayer.PlaybackSession.Position.TotalSeconds;
        if (currentSeconds >= ViewModel.TrimEndSeconds)
        {
            StopTrimPreviewAtEnd();
            return;
        }

        _isUpdatingPlayheadFromMedia = true;
        try
        {
            ViewModel.UpdatePlayhead(currentSeconds);
        }
        finally
        {
            _isUpdatingPlayheadFromMedia = false;
        }
    }

    private void VideoTrimRangeSlider_StartSecondsChanged(object sender, double seconds)
    {
        ViewModel.UpdateTrimStart(seconds);
    }

    private void VideoTrimRangeSlider_EndSecondsChanged(object sender, double seconds)
    {
        ViewModel.UpdateTrimEnd(seconds);
    }

    private void VideoTrimRangeSlider_PlayheadSecondsChanged(object sender, double seconds)
    {
        ViewModel.UpdatePlayhead(seconds);
    }

    private void SyncMediaPositionToPlayhead()
    {
        if (_mediaPlayer is null || _isUpdatingPlayheadFromMedia)
        {
            return;
        }

        if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing &&
            ViewModel.PlayheadSeconds >= ViewModel.TrimEndSeconds)
        {
            StopTrimPreviewAtEnd();
            return;
        }

        _mediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(ViewModel.PlayheadSeconds);
    }

    private void StopTrimPreviewAtEnd()
    {
        PauseTrimPreview();
        ViewModel.UpdatePlayhead(ViewModel.TrimEndSeconds);
    }

    private void PauseTrimPreview()
    {
        _mediaPlayer?.Pause();
        _trimPlaybackTimer.Stop();
        UpdateTrimPreviewPlayIcon(false);
    }

    private void UpdateTrimPreviewPlayIcon(bool isPlaying)
    {
        TrimPreviewPlayIcon.Glyph = isPlaying ? "\uE769" : "\uE768";
    }

    private string FormatTime(double seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    private string FormatTrimDuration(double startSeconds, double endSeconds)
    {
        return FormatTime(Math.Max(0, endSeconds - startSeconds));
    }
}
