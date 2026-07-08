using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.AudioEdit;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System.Buffers.Binary;
using System.ComponentModel;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class AudioEditPage : AudioEditPageBase
{
    private const double WaveformBarDefaultSpacing = 4;
    private const double WaveformDefaultSurfaceHeight = 140;
    private const double WaveformDefaultMaxBarHeight = 132;
    private static readonly TimeSpan WaveformUpdateInterval = TimeSpan.FromMilliseconds(50);

    private MediaPlayer? _mediaPlayer;
    private string? _currentAudioPath;
    private TimeSpan _audioDuration;
    private WaveformTimeline? _waveformTimeline;
    private DispatcherQueueTimer? _waveformTimer;
    private TimeSpan? _resumePosition;
    private bool _isMediaPlaybackSuspended;

    public AudioEditPage()
    {
        InitializeComponent();
        _mediaPlayer = CreateMediaPlayer();
        AudioPlayer.SetMediaPlayer(_mediaPlayer);
        AudioPlayer.Loaded += AudioPlayer_Loaded;
        AudioPlayer.Unloaded += AudioPlayer_Unloaded;
    }

    private MediaPlayer CreateMediaPlayer()
    {
        var mediaPlayer = new MediaPlayer();
        mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
        mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        return mediaPlayer;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isMediaPlaybackSuspended = false;
        _resumePosition = null;
        PauseMediaPlayer();
        base.OnNavigatedFrom(e);
    }

    public void SuspendMediaPlayback()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _resumePosition = _mediaPlayer.PlaybackSession.Position;
        _isMediaPlaybackSuspended = true;
        StopAndClearMediaPlayer();
    }

    public void ResumeMediaPlayback()
    {
        if (!_isMediaPlaybackSuspended)
        {
            return;
        }

        _isMediaPlaybackSuspended = false;
        TryInitializeAudio();
    }

    private void AudioPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        AudioPlayer.SetMediaPlayer(null);
        StopAndClearMediaPlayer();
        DisposeWaveformTimer();
        DisposeMediaPlayer();
        _mediaPlayer = null;
    }

    private void PauseMediaPlayer()
    {
        StopWaveformTimer();
        _currentAudioPath = null;
        _mediaPlayer?.Pause();
    }

    private void StopAndClearMediaPlayer()
    {
        PauseMediaPlayer();

        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.Source = null;
    }

    private void DisposeMediaPlayer()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
        _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
        _mediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        _mediaPlayer.Source = null;
        _mediaPlayer.Dispose();
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
            _currentAudioPath = filePath;
            _audioDuration = TimeSpan.Zero;
            _waveformTimeline = null;
            StopWaveformTimer();
            ViewModel.SetWaveformLevels([]);
            UpdateWaveformSizing();
            UpdateWaveformPlayhead(TimeSpan.Zero);

            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            var mediaSource = MediaSource.CreateFromStorageFile(file);
            _mediaPlayer?.Source = mediaSource;
            _ = LoadWaveformAsync(filePath);
        }
        catch (Exception)
        {
            // Audio not ready yet or file doesn't exist
        }
    }

    private async Task LoadWaveformAsync(string filePath)
    {
        IReadOnlyList<double>? capturedLevels = ViewModel.GetCapturedWaveformLevels(filePath);
        WaveformTimeline timeline = capturedLevels is { Count: > 0 }
            ? WaveformTimeline.FromCapturedLevels(capturedLevels, WaveformUpdateInterval, _audioDuration)
            : await Task.Run(() => AudioWaveformFileReader.ReadPeakTimeline(filePath, WaveformUpdateInterval));

        DispatcherQueue.TryEnqueue(() =>
        {
            if (string.Equals(_currentAudioPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _waveformTimeline = timeline;
                ViewModel.SetWaveformLevels(timeline.Levels);
                UpdateWaveformSizing();

                if (_audioDuration <= TimeSpan.Zero)
                {
                    _audioDuration = timeline.Duration;
                }

                UpdateWaveformPlayhead(_mediaPlayer?.PlaybackSession.Position ?? TimeSpan.Zero);
            }
        });
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _audioDuration = sender.PlaybackSession.NaturalDuration;
            if (_resumePosition is { } resumePosition)
            {
                sender.PlaybackSession.Position = _audioDuration > TimeSpan.Zero && resumePosition < _audioDuration
                    ? resumePosition
                    : TimeSpan.Zero;
                _resumePosition = null;
            }

            AlignCapturedWaveformTimelineToDuration(_audioDuration);
            UpdateWaveformPlayhead(sender.PlaybackSession.Position);
            UpdateWaveformTimer(sender.PlaybackSession);
        });
    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StopWaveformTimer();
            UpdateWaveformPlayhead(GetAudioDuration());
        });
    }

    private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() => UpdateWaveformPlayhead(sender.Position));
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() => UpdateWaveformTimer(sender));
    }

    private void WaveformTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_mediaPlayer is null)
        {
            StopWaveformTimer();
            return;
        }

        UpdateWaveformPlayhead(_mediaPlayer.PlaybackSession.Position);
    }

    private void WaveformSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWaveformSizing();
        UpdateWaveformPlayhead(_mediaPlayer?.PlaybackSession.Position ?? TimeSpan.Zero);
    }

    private void WaveformBarsRepeater_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWaveformPlayhead(_mediaPlayer?.PlaybackSession.Position ?? TimeSpan.Zero);
    }

    private void UpdateWaveformSizing()
    {
        int barCount = ViewModel.WaveformBars.Count;
        if (barCount == 0)
        {
            return;
        }

        double scale = GetWaveformHorizontalScale(barCount);
        double barWidth = AudioWaveformBarViewModel.DefaultWidth * scale;
        double spacing = WaveformBarDefaultSpacing * scale;
        double maxBarHeight = GetWaveformMaxBarHeight();

        if (WaveformBarsRepeater.Layout is StackLayout layout)
        {
            layout.Spacing = spacing;
        }

        foreach (var bar in ViewModel.WaveformBars)
        {
            bar.Width = barWidth;
            bar.Height = bar.Level * maxBarHeight;
        }
    }

    private double GetWaveformMaxBarHeight()
    {
        double surfaceHeight = WaveformSurface.ActualHeight;
        if (surfaceHeight <= 0)
        {
            return WaveformDefaultMaxBarHeight;
        }

        return surfaceHeight * (WaveformDefaultMaxBarHeight / WaveformDefaultSurfaceHeight);
    }

    private void WaveformSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        TimeSpan duration = GetAudioDuration();
        if (_mediaPlayer is null || duration <= TimeSpan.Zero || WaveformSurface.ActualWidth <= 0)
        {
            return;
        }

        WaveformTrackBounds trackBounds = GetWaveformTrackBounds();
        if (trackBounds.Width <= 0)
        {
            return;
        }

        double pointerX = e.GetCurrentPoint(WaveformSurface).Position.X;
        double progress = Math.Clamp((pointerX - trackBounds.Left) / trackBounds.Width, 0, 1);
        TimeSpan position = TimeSpan.FromTicks((long)(duration.Ticks * progress));
        _mediaPlayer.PlaybackSession.Position = position;
        UpdateWaveformPlayhead(position);
    }

    private void UpdateWaveformPlayhead(TimeSpan position)
    {
        TimeSpan duration = GetAudioDuration();
        double progress = duration > TimeSpan.Zero
            ? Math.Clamp(position.TotalMilliseconds / duration.TotalMilliseconds, 0, 1)
            : 0;

        WaveformTrackBounds trackBounds = GetWaveformTrackBounds();
        double trackWidth = Math.Max(0, trackBounds.Width - WaveformPlayhead.ActualWidth);
        WaveformPlayheadTransform.X = trackBounds.Left + (trackWidth * progress);
    }

    private WaveformTrackBounds GetWaveformTrackBounds()
    {
        double width = GetWaveformTrackWidth();

        double left = Math.Max(0, (WaveformSurface.ActualWidth - width) / 2);
        return new WaveformTrackBounds(left, width);
    }

    private double GetWaveformTrackWidth()
    {
        int barCount = ViewModel.WaveformBars.Count;
        double naturalWidth = GetWaveformNaturalWidth(barCount);
        double availableWidth = Math.Max(0, WaveformSurface.ActualWidth);

        if (naturalWidth <= 0)
        {
            return availableWidth;
        }

        return availableWidth > 0
            ? Math.Min(naturalWidth, availableWidth)
            : naturalWidth;
    }

    private double GetWaveformHorizontalScale(int barCount)
    {
        double naturalWidth = GetWaveformNaturalWidth(barCount);
        double availableWidth = Math.Max(0, WaveformSurface.ActualWidth);
        if (naturalWidth <= 0 || availableWidth <= 0)
        {
            return 1;
        }

        return Math.Min(1, availableWidth / naturalWidth);
    }

    private static double GetWaveformNaturalWidth(int barCount)
    {
        if (barCount <= 0)
        {
            return 0;
        }

        return (barCount * AudioWaveformBarViewModel.DefaultWidth) +
            ((barCount - 1) * WaveformBarDefaultSpacing);
    }

    private void UpdateWaveformTimer(MediaPlaybackSession playbackSession)
    {
        if (playbackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            StartWaveformTimer();
        }
        else
        {
            StopWaveformTimer();
        }
    }

    private void StartWaveformTimer()
    {
        _waveformTimer ??= CreateWaveformTimer();

        if (!_waveformTimer.IsRunning)
        {
            _waveformTimer.Start();
        }
    }

    private void StopWaveformTimer()
    {
        _waveformTimer?.Stop();
    }

    private DispatcherQueueTimer CreateWaveformTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = WaveformUpdateInterval;
        timer.IsRepeating = true;
        timer.Tick += WaveformTimer_Tick;
        return timer;
    }

    private void DisposeWaveformTimer()
    {
        if (_waveformTimer is null)
        {
            return;
        }

        _waveformTimer.Stop();
        _waveformTimer.Tick -= WaveformTimer_Tick;
        _waveformTimer = null;
    }

    private void AlignCapturedWaveformTimelineToDuration(TimeSpan duration)
    {
        if (_waveformTimeline is not { ShouldStretchToDuration: true } timeline || duration <= TimeSpan.Zero)
        {
            return;
        }

        _waveformTimeline = timeline.WithDuration(duration);
    }

    private TimeSpan GetAudioDuration()
    {
        TimeSpan naturalDuration = _mediaPlayer?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;
        if (naturalDuration > TimeSpan.Zero)
        {
            return naturalDuration;
        }

        TimeSpan waveformDuration = _waveformTimeline?.Duration ?? TimeSpan.Zero;
        return _audioDuration > TimeSpan.Zero ? _audioDuration : waveformDuration;
    }

    private readonly record struct WaveformTrackBounds(double Left, double Width);

    private readonly record struct WaveformTimeline(
        IReadOnlyList<double> Levels,
        TimeSpan Interval,
        TimeSpan Duration,
        bool ShouldStretchToDuration)
    {
        public static WaveformTimeline FromCapturedLevels(IReadOnlyList<double> levels, TimeSpan interval, TimeSpan duration)
        {
            TimeSpan fallbackDuration = TimeSpan.FromTicks(interval.Ticks * levels.Count);
            TimeSpan alignedDuration = duration > TimeSpan.Zero ? duration : fallbackDuration;
            return new WaveformTimeline(levels, interval, alignedDuration, true);
        }

        public WaveformTimeline WithDuration(TimeSpan duration)
            => this with { Duration = duration };
    }

    private static class AudioWaveformFileReader
    {
        public static WaveformTimeline ReadPeakTimeline(string filePath, TimeSpan interval)
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using BinaryReader reader = new(stream);

                if (ReadFourCc(reader) != "RIFF")
                {
                    return CreateEmptyTimeline(interval);
                }

                _ = reader.ReadUInt32();
                if (ReadFourCc(reader) != "WAVE")
                {
                    return CreateEmptyTimeline(interval);
                }

                WaveFormat? format = null;
                long dataOffset = 0;
                long dataLength = 0;

                while (stream.Position + 8 <= stream.Length)
                {
                    string chunkId = ReadFourCc(reader);
                    uint chunkSize = reader.ReadUInt32();
                    long chunkDataOffset = stream.Position;

                    if (chunkId == "fmt ")
                    {
                        format = ReadWaveFormat(reader, chunkSize);
                    }
                    else if (chunkId == "data")
                    {
                        dataOffset = chunkDataOffset;
                        dataLength = chunkSize;
                    }

                    stream.Position = chunkDataOffset + chunkSize + (chunkSize % 2);
                }

                if (format is null || dataOffset == 0 || dataLength == 0)
                {
                    return CreateEmptyTimeline(interval);
                }

                return ReadPeakTimeline(stream, format.Value, dataOffset, dataLength, interval);
            }
            catch
            {
                return CreateEmptyTimeline(interval);
            }
        }

        private static WaveformTimeline ReadPeakTimeline(
            FileStream stream,
            WaveFormat format,
            long dataOffset,
            long dataLength,
            TimeSpan interval)
        {
            if (!format.IsSupported || format.BlockAlign <= 0 || format.SampleRate <= 0 || interval <= TimeSpan.Zero)
            {
                return CreateEmptyTimeline(interval);
            }

            long totalFrames = dataLength / format.BlockAlign;
            if (totalFrames <= 0)
            {
                return CreateEmptyTimeline(interval);
            }

            long framesPerBucket = Math.Max(1, (long)Math.Round(format.SampleRate * interval.TotalSeconds));
            int bucketCount = (int)Math.Min(int.MaxValue, ((totalFrames + framesPerBucket - 1) / framesPerBucket));
            double[] peaks = new double[bucketCount];
            stream.Position = dataOffset;

            int bufferSize = Math.Max(format.BlockAlign, (64 * 1024 / format.BlockAlign) * format.BlockAlign);
            byte[] buffer = new byte[bufferSize];
            long framesRead = 0;
            long remainingBytes = dataLength;

            while (remainingBytes > 0)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
                int bytesRead = stream.Read(buffer, 0, bytesToRead);
                if (bytesRead <= 0)
                {
                    break;
                }

                int frameCount = bytesRead / format.BlockAlign;
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    int barIndex = (int)Math.Min(bucketCount - 1, framesRead / framesPerBucket);
                    int frameOffset = frameIndex * format.BlockAlign;
                    double peak = GetFramePeak(buffer.AsSpan(frameOffset, format.BlockAlign), format);
                    peaks[barIndex] = Math.Max(peaks[barIndex], peak);
                    framesRead++;
                }

                remainingBytes -= bytesRead;
            }

            TimeSpan duration = TimeSpan.FromSeconds(totalFrames / (double)format.SampleRate);
            return new WaveformTimeline(peaks, interval, duration, false);
        }

        private static WaveFormat ReadWaveFormat(BinaryReader reader, uint chunkSize)
        {
            ushort audioFormat = reader.ReadUInt16();
            ushort channels = reader.ReadUInt16();
            uint sampleRate = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            ushort blockAlign = reader.ReadUInt16();
            ushort bitsPerSample = reader.ReadUInt16();

            if (audioFormat == 0xFFFE && chunkSize >= 40)
            {
                ushort extensionSize = reader.ReadUInt16();
                _ = reader.ReadUInt16();
                _ = reader.ReadUInt32();
                byte[] subFormat = reader.ReadBytes(16);
                if (extensionSize >= 22 && subFormat.Length == 16)
                {
                    audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(subFormat);
                }
            }

            return new WaveFormat(audioFormat, channels, sampleRate, blockAlign, bitsPerSample);
        }

        private static double GetFramePeak(ReadOnlySpan<byte> frame, WaveFormat format)
        {
            int bytesPerSample = format.BitsPerSample / 8;
            double peak = 0;

            for (int channel = 0; channel < format.Channels; channel++)
            {
                int offset = channel * bytesPerSample;
                if (offset + bytesPerSample > frame.Length)
                {
                    break;
                }

                peak = Math.Max(peak, Math.Abs(ReadSample(frame[offset..], format)));
            }

            return Math.Clamp(peak, 0, 1);
        }

        private static double ReadSample(ReadOnlySpan<byte> sample, WaveFormat format)
        {
            return format.AudioFormat switch
            {
                1 => ReadPcmSample(sample, format.BitsPerSample),
                3 when format.BitsPerSample == 32 => ReadFloatSample(sample),
                _ => 0
            };
        }

        private static double ReadPcmSample(ReadOnlySpan<byte> sample, ushort bitsPerSample)
        {
            return bitsPerSample switch
            {
                8 => (sample[0] - 128) / 128d,
                16 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768d,
                24 => ReadSigned24BitSample(sample) / 8388608d,
                32 => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648d,
                _ => 0
            };
        }

        private static int ReadSigned24BitSample(ReadOnlySpan<byte> sample)
        {
            int value = sample[0] | (sample[1] << 8) | (sample[2] << 16);
            if ((value & 0x800000) != 0)
            {
                value |= unchecked((int)0xFF000000);
            }

            return value;
        }

        private static double ReadFloatSample(ReadOnlySpan<byte> sample)
        {
            float value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample));
            return float.IsFinite(value) ? value : 0;
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            Span<byte> bytes = stackalloc byte[4];
            if (reader.Read(bytes) != bytes.Length)
            {
                return string.Empty;
            }

            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        private static WaveformTimeline CreateEmptyTimeline(TimeSpan interval)
            => new([], interval, TimeSpan.Zero, false);

        private readonly record struct WaveFormat(
            ushort AudioFormat,
            ushort Channels,
            uint SampleRate,
            ushort BlockAlign,
            ushort BitsPerSample)
        {
            public bool IsSupported =>
                Channels > 0 &&
                SampleRate > 0 &&
                BlockAlign > 0 &&
                BitsPerSample is 8 or 16 or 24 or 32 &&
                (AudioFormat == 1 || AudioFormat == 3 && BitsPerSample == 32);
        }
    }
}
