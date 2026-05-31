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
    private MediaPlayer? _originalMediaPlayer;
    private MediaPlayer? _renderedTrimMediaPlayer;
    private string? _originalVideoPath;
    private string? _renderedTrimPreviewPath;
    private bool _hasLoadedOriginalDuration;
    private bool _isUpdatingPlayheadFromMedia;
    private bool _isSyncingMediaPosition;
    private int _renderedTrimPreviewVersion;

    public VideoEditPage()
    {
        InitializeComponent();
        _storageService = App.Current.ServiceProvider.GetService<IStorageService>();
        _videoFileTrimmer = App.Current.ServiceProvider.GetService<IVideoFileTrimmer>();

        _originalMediaPlayer = CreateMediaPlayer();
        OriginalVideoPlayer.SetMediaPlayer(_originalMediaPlayer);

        _renderedTrimMediaPlayer = CreateMediaPlayer();
        RenderedTrimVideoPlayer.SetMediaPlayer(_renderedTrimMediaPlayer);

        OriginalVideoPlayer.Loaded += VideoPlayer_Loaded;
        OriginalVideoPlayer.Unloaded += VideoPlayer_Unloaded;

        _boundedPlaybackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _boundedPlaybackTimer.Tick += BoundedPlaybackTimer_Tick;
    }

    private MediaPlayer CreateMediaPlayer()
    {
        var mediaPlayer = new MediaPlayer();
        mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
        mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        return mediaPlayer;
    }

    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _boundedPlaybackTimer.Stop();
        DisposeMediaPlayer(ref _originalMediaPlayer);
        DisposeMediaPlayer(ref _renderedTrimMediaPlayer);
        DeleteRenderedTrimPreview();
    }

    private void DisposeMediaPlayer(ref MediaPlayer? mediaPlayer)
    {
        if (mediaPlayer is null)
        {
            return;
        }

        mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
        mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
        mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        mediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
        mediaPlayer.Dispose();
        mediaPlayer = null;
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
            _ = UpdateRenderedTrimPreviewAsync();
        }

        if (e.PropertyName == nameof(VideoEditPageViewModel.TrimStartSeconds) ||
            e.PropertyName == nameof(VideoEditPageViewModel.TrimEndSeconds))
        {
            _renderedTrimPreviewVersion++;
            DeleteRenderedTrimPreview();
            if (ViewModel.IsInTrimMode)
            {
                ShowOriginalPlayer();
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
            _renderedTrimPreviewVersion++;
            DeleteRenderedTrimPreview();
            await LoadMediaSourceAsync(_originalMediaPlayer, filePath);
            ShowOriginalPlayer();
        }
        catch (Exception)
        {
            // Video not ready yet or file doesn't exist.
        }
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (sender == _originalMediaPlayer && !_hasLoadedOriginalDuration)
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
                PauseInactivePlayer();
                EnsurePlaybackIsInsideTrimRange();
                _boundedPlaybackTimer.Start();
                UpdateTrimPreviewPlayIcon(true);
            }
            else if (ActiveMediaPlayer?.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                _boundedPlaybackTimer.Stop();
                UpdateTrimPreviewPlayIcon(false);
            }
        });
    }

    private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
    {
        if (_isSyncingMediaPosition || sender != ActiveMediaPlayer?.PlaybackSession)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (ActiveMediaPlayer is null || _isSyncingMediaPosition)
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
        if (ActiveMediaPlayer is null)
        {
            return;
        }

        if (ActiveMediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            PauseTrimPreview();
            return;
        }

        if (ViewModel.PlayheadSeconds >= ViewModel.TrimEndSeconds)
        {
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
        }

        SyncMediaPositionToPlayhead();
        ActiveMediaPlayer.Play();
        _boundedPlaybackTimer.Start();
        UpdateTrimPreviewPlayIcon(true);
    }

    private void BoundedPlaybackTimer_Tick(object? sender, object e)
    {
        if (ActiveMediaPlayer is null)
        {
            _boundedPlaybackTimer.Stop();
            return;
        }

        double currentSeconds = ActiveMediaPlayer.PlaybackSession.Position.TotalSeconds;
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
        if (ActiveMediaPlayer is null || _isUpdatingPlayheadFromMedia)
        {
            return;
        }

        if (ActiveMediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing &&
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
        _originalMediaPlayer?.Pause();
        _renderedTrimMediaPlayer?.Pause();
        _boundedPlaybackTimer.Stop();
        UpdateTrimPreviewPlayIcon(false);
    }

    private void PauseInactivePlayer()
    {
        if (ActiveMediaPlayer == _originalMediaPlayer)
        {
            _renderedTrimMediaPlayer?.Pause();
        }
        else
        {
            _originalMediaPlayer?.Pause();
        }
    }

    private void EnsurePlaybackIsInsideTrimRange()
    {
        if (ActiveMediaPlayer is null)
        {
            return;
        }

        double currentSeconds = ActiveMediaPlayer.PlaybackSession.Position.TotalSeconds;
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
        if (ActiveMediaPlayer is null)
        {
            return;
        }

        _isSyncingMediaPosition = true;
        try
        {
            ActiveMediaPlayer.PlaybackSession.Position = position;
        }
        finally
        {
            _isSyncingMediaPosition = false;
        }
    }

    private async Task UpdateRenderedTrimPreviewAsync()
    {
        if (ViewModel.IsInTrimMode)
        {
            ShowOriginalPlayer();
            return;
        }

        if (!ViewModel.IsTrimmed || string.IsNullOrEmpty(_originalVideoPath))
        {
            ShowOriginalPlayer();
            return;
        }

        int renderVersion = ++_renderedTrimPreviewVersion;
        try
        {
            PauseTrimPreview();
            SetTrimPreviewLoading(true);

            _renderedTrimPreviewPath ??= GetRenderedTrimPreviewPath();
            await _videoFileTrimmer.TrimAsync(
                _originalVideoPath,
                _renderedTrimPreviewPath,
                TimeSpan.FromSeconds(ViewModel.TrimStartSeconds),
                TimeSpan.FromSeconds(ViewModel.TrimEndSeconds));

            if (renderVersion != _renderedTrimPreviewVersion || ViewModel.IsInTrimMode)
            {
                return;
            }

            await LoadMediaSourceAsync(_renderedTrimMediaPlayer, _renderedTrimPreviewPath);
            ViewModel.UpdatePlayhead(ViewModel.TrimStartSeconds);
            ShowRenderedTrimPlayer();
        }
        catch (Exception ex)
        {
            AppServiceLocator.Logging.LogException(ex, "Failed to render trimmed video preview.");
            ShowOriginalPlayer();
        }
        finally
        {
            if (renderVersion == _renderedTrimPreviewVersion)
            {
                SetTrimPreviewLoading(false);
            }
        }
    }

    private static async Task LoadMediaSourceAsync(MediaPlayer? mediaPlayer, string filePath)
    {
        if (mediaPlayer is null)
        {
            return;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        mediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
    }

    private void ShowOriginalPlayer()
    {
        _renderedTrimMediaPlayer?.Pause();
        OriginalVideoPlayer.Visibility = ViewModel.IsVideoReady ? Visibility.Visible : Visibility.Collapsed;
        RenderedTrimVideoPlayer.Visibility = Visibility.Collapsed;
        SyncMediaPositionToPlayhead();
    }

    private void ShowRenderedTrimPlayer()
    {
        _originalMediaPlayer?.Pause();
        OriginalVideoPlayer.Visibility = Visibility.Collapsed;
        RenderedTrimVideoPlayer.Visibility = ViewModel.IsVideoReady ? Visibility.Visible : Visibility.Collapsed;
        SyncMediaPositionToPlayhead();
    }

    private void SetTrimPreviewLoading(bool isLoading)
    {
        TrimPreviewLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
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

    private bool IsRenderedTrimPlayerVisible => RenderedTrimVideoPlayer.Visibility == Visibility.Visible;

    private MediaPlayer? ActiveMediaPlayer => IsRenderedTrimPlayerVisible ? _renderedTrimMediaPlayer : _originalMediaPlayer;

    private double PlaybackStartSeconds => IsRenderedTrimPlayerVisible ? 0 : ViewModel.TrimStartSeconds;

    private double PlaybackEndSeconds => IsRenderedTrimPlayerVisible
        ? Math.Max(0, ViewModel.TrimEndSeconds - ViewModel.TrimStartSeconds)
        : ViewModel.TrimEndSeconds;

    private double GetOriginalSecondsFromPlaybackSeconds(double seconds)
    {
        return IsRenderedTrimPlayerVisible ? seconds + ViewModel.TrimStartSeconds : seconds;
    }

    private double GetPlaybackSecondsFromOriginalSeconds(double seconds)
    {
        return IsRenderedTrimPlayerVisible ? seconds - ViewModel.TrimStartSeconds : seconds;
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
