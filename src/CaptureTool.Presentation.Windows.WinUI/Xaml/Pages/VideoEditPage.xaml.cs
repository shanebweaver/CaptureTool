using CaptureTool.Infrastructure.Abstractions.Media;
using CaptureTool.Infrastructure.Abstractions.Storage;
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
    private readonly IStorageService _storageService;
    private readonly IVideoFileTrimmer _videoFileTrimmer;
    private MediaPlayer? _mediaPlayer;
    private string? _originalVideoPath;
    private string? _renderedTrimPreviewPath;
    private bool _isUsingRenderedTrimPreview;
    private bool _hasLoadedOriginalDuration;
    private bool _isUpdatingPlayheadFromMedia;
    private bool _isSyncingMediaPosition;

    public VideoEditPage()
    {
        InitializeComponent();
        _storageService = App.Current.ServiceProvider.GetService<IStorageService>();
        _videoFileTrimmer = App.Current.ServiceProvider.GetService<IVideoFileTrimmer>();

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
        DeleteRenderedTrimPreview();
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

        if (e.PropertyName == nameof(VideoEditPageViewModel.IsInTrimMode))
        {
            _ = UpdateRenderedTrimPreviewSourceAsync();
        }

        if (e.PropertyName == nameof(VideoEditPageViewModel.TrimStartSeconds) ||
            e.PropertyName == nameof(VideoEditPageViewModel.TrimEndSeconds))
        {
            DeleteRenderedTrimPreview();
            if (ViewModel.IsInTrimMode && _isUsingRenderedTrimPreview)
            {
                _ = LoadOriginalVideoAsync();
            }
            SyncMediaPositionToPlayhead();
        }

        if (e.PropertyName == nameof(VideoEditPageViewModel.PlayheadSeconds))
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
            _originalVideoPath = filePath;
            _hasLoadedOriginalDuration = false;
            DeleteRenderedTrimPreview();
            await LoadMediaSourceAsync(filePath, isRenderedTrimPreview: false);
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
            if (!_isUsingRenderedTrimPreview && !_hasLoadedOriginalDuration)
            {
                ViewModel.SetVideoDuration(sender.PlaybackSession.NaturalDuration);
                _hasLoadedOriginalDuration = true;
            }
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
            if (currentSeconds < PlaybackStartSeconds)
            {
                UpdatePlayheadFromMedia(PlaybackStartSeconds);
                SetMediaPosition(TimeSpan.FromSeconds(PlaybackStartSeconds));
                return;
            }

            if (currentSeconds > PlaybackEndSeconds)
            {
                if (sender.PlaybackState == MediaPlaybackState.Playing)
                {
                    StopPlaybackAtTrimEnd();
                }
                else
                {
                    UpdatePlayheadFromMedia(PlaybackEndSeconds);
                    SetMediaPosition(TimeSpan.FromSeconds(PlaybackEndSeconds));
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
        if (currentSeconds < PlaybackStartSeconds)
        {
            UpdatePlayheadFromMedia(PlaybackStartSeconds);
            SetMediaPosition(TimeSpan.FromSeconds(PlaybackStartSeconds));
            return;
        }

        if (currentSeconds >= PlaybackEndSeconds)
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
            GetPlaybackSecondsFromOriginalSeconds(ViewModel.PlayheadSeconds) >= PlaybackEndSeconds)
        {
            StopPlaybackAtTrimEnd();
            return;
        }

        SetMediaPosition(TimeSpan.FromSeconds(GetPlaybackSecondsFromOriginalSeconds(ViewModel.PlayheadSeconds)));
    }

    private void StopPlaybackAtTrimEnd()
    {
        PauseTrimPreview();
        UpdatePlayheadFromMedia(PlaybackEndSeconds);
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
        if (currentSeconds < PlaybackStartSeconds || currentSeconds >= PlaybackEndSeconds)
        {
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
            SetMediaPosition(TimeSpan.FromSeconds(PlaybackStartSeconds));
        }
        else
        {
            UpdatePlayheadFromMedia(currentSeconds);
        }
    }

    private void UpdatePlayheadFromMedia(double seconds)
    {
        _isUpdatingPlayheadFromMedia = true;
        try
        {
            ViewModel.UpdatePlayhead(GetOriginalSecondsFromPlaybackSeconds(seconds));
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

    private async Task UpdateRenderedTrimPreviewSourceAsync()
    {
        if (ViewModel.IsInTrimMode)
        {
            if (_isUsingRenderedTrimPreview)
            {
                await LoadOriginalVideoAsync();
            }
            return;
        }

        if (!ViewModel.IsTrimmed || string.IsNullOrEmpty(_originalVideoPath))
        {
            if (_isUsingRenderedTrimPreview)
            {
                await LoadOriginalVideoAsync();
            }
            return;
        }

        try
        {
            PauseTrimPreview();
            _renderedTrimPreviewPath ??= GetRenderedTrimPreviewPath();
            await _videoFileTrimmer.TrimAsync(
                _originalVideoPath,
                _renderedTrimPreviewPath,
                TimeSpan.FromSeconds(ViewModel.TrimStartSeconds),
                TimeSpan.FromSeconds(ViewModel.TrimEndSeconds));

            await LoadMediaSourceAsync(_renderedTrimPreviewPath, isRenderedTrimPreview: true);
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
        }
        catch (Exception ex)
        {
            AppServiceLocator.Logging.LogException(ex, "Failed to render trimmed video preview.");
            if (_isUsingRenderedTrimPreview)
            {
                await LoadOriginalVideoAsync();
            }
        }
    }

    private async Task LoadOriginalVideoAsync()
    {
        if (string.IsNullOrEmpty(_originalVideoPath))
        {
            return;
        }

        await LoadMediaSourceAsync(_originalVideoPath, isRenderedTrimPreview: false);
        SyncMediaPositionToPlayhead();
    }

    private async Task LoadMediaSourceAsync(string filePath, bool isRenderedTrimPreview)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        var mediaSource = MediaSource.CreateFromStorageFile(file);
        if (_mediaPlayer is not null)
        {
            _isUsingRenderedTrimPreview = isRenderedTrimPreview;
            _mediaPlayer.Source = mediaSource;
        }
    }

    private string GetRenderedTrimPreviewPath()
    {
        return Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}.mp4");
    }

    private void DeleteRenderedTrimPreview()
    {
        if (string.IsNullOrEmpty(_renderedTrimPreviewPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_renderedTrimPreviewPath))
            {
                File.Delete(_renderedTrimPreviewPath);
            }
        }
        catch (Exception ex)
        {
            AppServiceLocator.Logging.LogException(ex, "Failed to delete rendered trim preview.");
        }
        finally
        {
            _renderedTrimPreviewPath = null;
        }
    }

    private double PlaybackStartSeconds => _isUsingRenderedTrimPreview ? 0 : ViewModel.TrimStartSeconds;

    private double PlaybackEndSeconds => _isUsingRenderedTrimPreview
        ? Math.Max(0, ViewModel.TrimEndSeconds - ViewModel.TrimStartSeconds)
        : ViewModel.TrimEndSeconds;

    private double GetOriginalSecondsFromPlaybackSeconds(double seconds)
    {
        return _isUsingRenderedTrimPreview ? seconds + ViewModel.TrimStartSeconds : seconds;
    }

    private double GetPlaybackSecondsFromOriginalSeconds(double seconds)
    {
        return _isUsingRenderedTrimPreview ? seconds - ViewModel.TrimStartSeconds : seconds;
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
