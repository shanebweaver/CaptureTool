using CaptureTool.Presentation.Features.VideoEdit;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class VideoEditPage : VideoEditPageBase
{
    private readonly DispatcherTimer _boundedPlaybackTimer;
    private MediaPlayer? _mediaPlayer;
    private bool _isUpdatingPlayheadFromMedia;
    private bool _isSyncingMediaPosition;

    public VideoEditPage()
    {
        InitializeComponent();
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
        _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        _mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        VideoPlayer.SetMediaPlayer(_mediaPlayer);
        VideoPlayer.Loaded += VideoPlayer_Loaded;
        VideoPlayer.Unloaded += VideoPlayer_Unloaded;

        _boundedPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _boundedPlaybackTimer.Tick += BoundedPlaybackTimer_Tick;
    }

    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _boundedPlaybackTimer.Stop();
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            _mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            _mediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
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
        DispatcherQueue.TryEnqueue(StopPlaybackAtTrimEnd);
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing)
            {
                EnsurePlaybackIsInsideTrimRange();
                _boundedPlaybackTimer.Start();
                UpdateTrimPreviewPlayIcon(true);
            }
            else
            {
                _boundedPlaybackTimer.Stop();
                UpdateTrimPreviewPlayIcon(false);
            }
        });
    }

    private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
    {
        if (_isSyncingMediaPosition)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_mediaPlayer is null || _isSyncingMediaPosition)
            {
                return;
            }

            double currentSeconds = sender.Position.TotalSeconds;
            if (currentSeconds < ViewModel.TrimStartSeconds)
            {
                UpdatePlayheadFromMedia(ViewModel.TrimStartSeconds);
                SetMediaPosition(TimeSpan.FromSeconds(ViewModel.TrimStartSeconds));
                return;
            }

            if (currentSeconds > ViewModel.TrimEndSeconds)
            {
                if (sender.PlaybackState == MediaPlaybackState.Playing)
                {
                    StopPlaybackAtTrimEnd();
                }
                else
                {
                    UpdatePlayheadFromMedia(ViewModel.TrimEndSeconds);
                    SetMediaPosition(TimeSpan.FromSeconds(ViewModel.TrimEndSeconds));
                }

                return;
            }

            UpdatePlayheadFromMedia(currentSeconds);
        });
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
        _boundedPlaybackTimer.Start();
        UpdateTrimPreviewPlayIcon(true);
    }

    private void BoundedPlaybackTimer_Tick(object? sender, object e)
    {
        if (_mediaPlayer is null)
        {
            _boundedPlaybackTimer.Stop();
            return;
        }

        double currentSeconds = _mediaPlayer.PlaybackSession.Position.TotalSeconds;
        if (currentSeconds < ViewModel.TrimStartSeconds)
        {
            UpdatePlayheadFromMedia(ViewModel.TrimStartSeconds);
            SetMediaPosition(TimeSpan.FromSeconds(ViewModel.TrimStartSeconds));
            return;
        }

        if (currentSeconds >= ViewModel.TrimEndSeconds)
        {
            StopPlaybackAtTrimEnd();
            return;
        }

        UpdatePlayheadFromMedia(currentSeconds);
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
            StopPlaybackAtTrimEnd();
            return;
        }

        SetMediaPosition(TimeSpan.FromSeconds(ViewModel.PlayheadSeconds));
    }

    private void StopPlaybackAtTrimEnd()
    {
        PauseTrimPreview();
        ViewModel.UpdatePlayhead(ViewModel.TrimEndSeconds);
    }

    private void PauseTrimPreview()
    {
        _mediaPlayer?.Pause();
        _boundedPlaybackTimer.Stop();
        UpdateTrimPreviewPlayIcon(false);
    }

    private void EnsurePlaybackIsInsideTrimRange()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        double currentSeconds = _mediaPlayer.PlaybackSession.Position.TotalSeconds;
        if (currentSeconds < ViewModel.TrimStartSeconds || currentSeconds >= ViewModel.TrimEndSeconds)
        {
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
            SetMediaPosition(TimeSpan.FromSeconds(ViewModel.TrimStartSeconds));
        }
        else
        {
            ViewModel.UpdatePlayhead(currentSeconds);
        }
    }

    private void UpdatePlayheadFromMedia(double seconds)
    {
        _isUpdatingPlayheadFromMedia = true;
        try
        {
            ViewModel.UpdatePlayhead(seconds);
        }
        finally
        {
            _isUpdatingPlayheadFromMedia = false;
        }
    }

    private void SetMediaPosition(TimeSpan position)
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _isSyncingMediaPosition = true;
        try
        {
            _mediaPlayer.PlaybackSession.Position = position;
        }
        finally
        {
            _isSyncingMediaPosition = false;
        }
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
