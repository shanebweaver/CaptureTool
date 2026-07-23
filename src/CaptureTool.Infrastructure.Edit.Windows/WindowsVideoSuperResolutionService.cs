using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.FileSystem;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Video;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsVideoSuperResolutionService : IVideoSuperResolutionService
{
    private const double DefaultScaleFactor = 2.0;
    private const int MaxOutputLongEdge = 3840;
    private const int MaxOutputShortEdge = 2160;
    private const int MinInputShortEdge = 240;
    private const int MaxInputShortEdge = 1440;
    private const double MinSupportedFrameRate = 15;
    private const double MaxSupportedFrameRate = 60;

    private readonly IStorageService _storageService;

    public WindowsVideoSuperResolutionService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public VideoSuperResolutionReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(VideoScaler.GetReadyState());
        }
        catch
        {
            return VideoSuperResolutionReadyState.Unknown;
        }
    }

    public async Task<VideoSuperResolutionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        VideoSuperResolutionReadyState readyState = GetReadyState();
        if (readyState == VideoSuperResolutionReadyState.Ready)
        {
            return VideoSuperResolutionPreparationResult.Success;
        }

        if (readyState is VideoSuperResolutionReadyState.NotSupported or VideoSuperResolutionReadyState.Disabled)
        {
            return VideoSuperResolutionPreparationResult.NotSupported;
        }

        try
        {
            AIFeatureReadyResult result = await VideoScaler.EnsureReadyAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                return VideoSuperResolutionPreparationResult.Cancelled;
            }

            return result.Status == AIFeatureReadyResultState.Success
                ? VideoSuperResolutionPreparationResult.Success
                : VideoSuperResolutionPreparationResult.Failed(GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return VideoSuperResolutionPreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return VideoSuperResolutionPreparationResult.Failed(ex.Message);
        }
    }

    public async Task<VideoSuperResolutionResult> GenerateAsync(
        VideoSuperResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetReadyState() != VideoSuperResolutionReadyState.Ready)
        {
            return VideoSuperResolutionResult.NotReady;
        }

        string? videoOnlyPath = null;
        try
        {
            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(request.SourceVideo.FilePath);
            MediaClip sourceClip = await MediaClip.CreateFromFileAsync(sourceFile);
            var sourceComposition = new MediaComposition();
            sourceComposition.Clips.Add(sourceClip);
            VideoEncodingProperties sourceProperties = sourceClip.GetVideoEncodingProperties();
            double frameRate = GetFrameRate(sourceProperties);

            if (!IsSupportedInput(sourceProperties.Width, sourceProperties.Height, frameRate))
            {
                return VideoSuperResolutionResult.UnsupportedVideo();
            }

            (int targetWidth, int targetHeight) = CalculateTargetSize(
                checked((int)sourceProperties.Width),
                checked((int)sourceProperties.Height),
                request.ScaleFactor <= 0 ? DefaultScaleFactor : request.ScaleFactor);

            if (targetWidth <= sourceProperties.Width || targetHeight <= sourceProperties.Height)
            {
                return VideoSuperResolutionResult.UnsupportedVideo();
            }

            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(
                _storageService.GetApplicationTemporaryFolderPath());
            string outputFileName = GetOutputFileName(request.SourceVideo.FilePath);
            string videoOnlyFileName = $"{Path.GetFileNameWithoutExtension(outputFileName)}.video.mp4";
            StorageFile videoOnlyFile = await outputFolder.CreateFileAsync(
                videoOnlyFileName,
                CreationCollisionOption.ReplaceExisting);
            videoOnlyPath = videoOnlyFile.Path;

            cancellationToken.ThrowIfCancellationRequested();

            using VideoScaler videoScaler = await VideoScaler.CreateAsync();
            var frameSource = new SuperResolutionFrameSource(
                sourceComposition,
                videoScaler,
                checked((int)sourceProperties.Width),
                checked((int)sourceProperties.Height),
                targetWidth,
                targetHeight,
                sourceClip.OriginalDuration,
                sourceProperties.FrameRate.Numerator,
                sourceProperties.FrameRate.Denominator,
                cancellationToken);

            MediaEncodingProfile encodingProfile = CreateEncodingProfile(
                targetWidth,
                targetHeight,
                sourceProperties.FrameRate.Numerator,
                sourceProperties.FrameRate.Denominator);
            await TranscodeFramesAsync(
                frameSource.MediaStreamSource,
                videoOnlyFile,
                encodingProfile,
                cancellationToken);
            frameSource.ThrowIfFailed();

            StorageFile outputFile = await outputFolder.CreateFileAsync(
                outputFileName,
                CreationCollisionOption.ReplaceExisting);
            await AddOriginalAudioAsync(
                sourceClip,
                videoOnlyFile,
                outputFile,
                encodingProfile,
                cancellationToken);

            return VideoSuperResolutionResult.Success(new VideoFile(outputFile.Path));
        }
        catch (OperationCanceledException)
        {
            return VideoSuperResolutionResult.Cancelled;
        }
        catch (Exception ex)
        {
            return VideoSuperResolutionResult.Failed(ex.Message);
        }
        finally
        {
            TryDeleteFile(videoOnlyPath);
        }
    }

    internal static (int Width, int Height) CalculateTargetSize(
        int sourceWidth,
        int sourceHeight,
        double requestedScaleFactor)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || requestedScaleFactor <= 0)
        {
            return (0, 0);
        }

        bool landscape = sourceWidth >= sourceHeight;
        int maxWidth = landscape ? MaxOutputLongEdge : MaxOutputShortEdge;
        int maxHeight = landscape ? MaxOutputShortEdge : MaxOutputLongEdge;
        double scaleFactor = Math.Min(
            requestedScaleFactor,
            Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight));

        return (
            RoundDownToEven(sourceWidth * scaleFactor),
            RoundDownToEven(sourceHeight * scaleFactor));
    }

    internal static string GetOutputFileName(string sourceFilePath)
    {
        return $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.super.mp4";
    }

    private static bool IsSupportedInput(uint width, uint height, double frameRate)
    {
        uint shortEdge = Math.Min(width, height);
        return width > 0 &&
            height > 0 &&
            shortEdge >= MinInputShortEdge &&
            shortEdge <= MaxInputShortEdge &&
            frameRate >= MinSupportedFrameRate &&
            frameRate <= MaxSupportedFrameRate;
    }

    private static double GetFrameRate(VideoEncodingProperties properties)
    {
        return properties.FrameRate.Denominator == 0
            ? 0
            : (double)properties.FrameRate.Numerator / properties.FrameRate.Denominator;
    }

    private static int RoundDownToEven(double value)
    {
        int rounded = Math.Max(2, (int)Math.Floor(value));
        return rounded - rounded % 2;
    }

    private static MediaEncodingProfile CreateEncodingProfile(
        int width,
        int height,
        uint frameRateNumerator,
        uint frameRateDenominator)
    {
        MediaEncodingProfile profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
        profile.Video.Width = (uint)width;
        profile.Video.Height = (uint)height;
        profile.Video.FrameRate.Numerator = frameRateNumerator;
        profile.Video.FrameRate.Denominator = Math.Max(1, frameRateDenominator);
        profile.Video.Bitrate = CalculateBitrate(width, height, frameRateNumerator, frameRateDenominator);
        return profile;
    }

    private static uint CalculateBitrate(
        int width,
        int height,
        uint frameRateNumerator,
        uint frameRateDenominator)
    {
        double frameRate = frameRateDenominator == 0
            ? 30
            : (double)frameRateNumerator / frameRateDenominator;
        double bitrate = width * (double)height * frameRate * 0.1;
        return (uint)Math.Clamp(bitrate, 4_000_000, 40_000_000);
    }

    private static async Task TranscodeFramesAsync(
        MediaStreamSource mediaStreamSource,
        StorageFile outputFile,
        MediaEncodingProfile encodingProfile,
        CancellationToken cancellationToken)
    {
        var transcoder = new MediaTranscoder
        {
            HardwareAccelerationEnabled = true
        };
        using IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        PrepareTranscodeResult prepareResult = await transcoder.PrepareMediaStreamSourceTranscodeAsync(
            mediaStreamSource,
            outputStream,
            encodingProfile);
        if (!prepareResult.CanTranscode)
        {
            throw new InvalidOperationException(
                $"Failed to prepare the super-resolution video encoder: {prepareResult.FailureReason}.");
        }

        await prepareResult.TranscodeAsync().AsTask(cancellationToken);
    }

    private static async Task AddOriginalAudioAsync(
        MediaClip sourceClip,
        StorageFile videoOnlyFile,
        StorageFile outputFile,
        MediaEncodingProfile encodingProfile,
        CancellationToken cancellationToken)
    {
        MediaClip enhancedClip = await MediaClip.CreateFromFileAsync(videoOnlyFile);
        var composition = new MediaComposition();
        composition.Clips.Add(enhancedClip);

        if (sourceClip.EmbeddedAudioTracks.Count > 0)
        {
            BackgroundAudioTrack audioTrack = BackgroundAudioTrack.CreateFromEmbeddedAudioTrack(
                sourceClip.EmbeddedAudioTracks[0]);
            if (audioTrack.OriginalDuration > enhancedClip.OriginalDuration)
            {
                audioTrack.TrimTimeFromEnd = audioTrack.OriginalDuration - enhancedClip.OriginalDuration;
            }

            composition.BackgroundAudioTracks.Add(audioTrack);
        }

        TranscodeFailureReason result = await composition.RenderToFileAsync(
            outputFile,
            MediaTrimmingPreference.Precise,
            encodingProfile).AsTask(cancellationToken);
        if (result != TranscodeFailureReason.None)
        {
            throw new InvalidOperationException(
                $"Failed to render the super-resolution video: {result}.");
        }
    }

    private static VideoSuperResolutionReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => VideoSuperResolutionReadyState.Ready,
            AIFeatureReadyState.NotReady => VideoSuperResolutionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem => VideoSuperResolutionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => VideoSuperResolutionReadyState.Disabled,
            _ => VideoSuperResolutionReadyState.Unknown
        };
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }

    private static void TryDeleteFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Temporary file cleanup is best effort.
        }
    }

    private sealed class SuperResolutionFrameSource
    {
        private readonly MediaComposition _sourceComposition;
        private readonly VideoScaler _videoScaler;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;
        private readonly int _targetWidth;
        private readonly int _targetHeight;
        private readonly TimeSpan _duration;
        private readonly TimeSpan _frameDuration;
        private readonly long _frameCount;
        private readonly CancellationToken _cancellationToken;
        private readonly SemaphoreSlim _sampleGate = new(1, 1);
        private long _nextFrameIndex;
        private Exception? _failure;

        public SuperResolutionFrameSource(
            MediaComposition sourceComposition,
            VideoScaler videoScaler,
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight,
            TimeSpan duration,
            uint frameRateNumerator,
            uint frameRateDenominator,
            CancellationToken cancellationToken)
        {
            _sourceComposition = sourceComposition;
            _videoScaler = videoScaler;
            _sourceWidth = sourceWidth;
            _sourceHeight = sourceHeight;
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;
            _duration = duration;
            _frameDuration = CalculateFrameDuration(frameRateNumerator, frameRateDenominator);
            _frameCount = Math.Max(1, (long)Math.Ceiling(duration.Ticks / (double)_frameDuration.Ticks));
            _cancellationToken = cancellationToken;

            VideoEncodingProperties properties = VideoEncodingProperties.CreateUncompressed(
                MediaEncodingSubtypes.Bgra8,
                (uint)targetWidth,
                (uint)targetHeight);
            var descriptor = new VideoStreamDescriptor(properties);
            MediaStreamSource = new MediaStreamSource(descriptor)
            {
                BufferTime = TimeSpan.Zero,
                Duration = duration
            };
            MediaStreamSource.Starting += MediaStreamSource_Starting;
            MediaStreamSource.SampleRequested += MediaStreamSource_SampleRequested;
        }

        public MediaStreamSource MediaStreamSource { get; }

        public void ThrowIfFailed()
        {
            if (_failure is not null)
            {
                throw new InvalidOperationException(
                    "Failed to generate a super-resolution video frame.",
                    _failure);
            }
        }

        private void MediaStreamSource_Starting(
            MediaStreamSource sender,
            MediaStreamSourceStartingEventArgs args)
        {
            TimeSpan startPosition = args.Request.StartPosition.GetValueOrDefault();
            _nextFrameIndex = Math.Clamp(
                (long)Math.Floor(startPosition.Ticks / (double)_frameDuration.Ticks),
                0,
                _frameCount);
            args.Request.SetActualStartPosition(TimeSpan.FromTicks(_nextFrameIndex * _frameDuration.Ticks));
        }

        private async void MediaStreamSource_SampleRequested(
            MediaStreamSource sender,
            MediaStreamSourceSampleRequestedEventArgs args)
        {
            MediaStreamSourceSampleRequestDeferral deferral = args.Request.GetDeferral();
            try
            {
                await _sampleGate.WaitAsync(_cancellationToken);
                try
                {
                    if (_failure is not null || _nextFrameIndex >= _frameCount)
                    {
                        return;
                    }

                    _cancellationToken.ThrowIfCancellationRequested();
                    long frameIndex = _nextFrameIndex++;
                    TimeSpan timestamp = TimeSpan.FromTicks(frameIndex * _frameDuration.Ticks);
                    IBuffer frameBuffer = await GenerateFrameAsync(timestamp);
                    MediaStreamSample sample = MediaStreamSample.CreateFromBuffer(frameBuffer, timestamp);
                    sample.Duration = _frameDuration;
                    sample.KeyFrame = true;
                    args.Request.Sample = sample;
                }
                finally
                {
                    _sampleGate.Release();
                }
            }
            catch (Exception ex)
            {
                _failure = ex;
                sender.NotifyError(MediaStreamSourceErrorStatus.Other);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async Task<IBuffer> GenerateFrameAsync(TimeSpan timestamp)
        {
            using IRandomAccessStream thumbnailStream = await _sourceComposition.GetThumbnailAsync(
                Min(timestamp, _duration),
                _sourceWidth,
                _sourceHeight,
                VideoFramePrecision.NearestFrame);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(thumbnailStream);
            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = (uint)_sourceWidth,
                    ScaledHeight = (uint)_sourceHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            byte[] sourceBgra = pixelData.DetachPixelData();
            byte[] sourceBgr = ConvertBgraToBgr(sourceBgra);
            byte[] targetBgr = new byte[checked(_targetWidth * _targetHeight * 3)];

            using ImageBuffer inputBuffer = ImageBuffer.CreateForBuffer(
                CreateBuffer(sourceBgr),
                ImageBufferPixelFormat.Bgr8,
                _sourceWidth,
                _sourceHeight,
                _sourceWidth * 3);
            using ImageBuffer outputBuffer = ImageBuffer.CreateForBuffer(
                CreateWritableBuffer(targetBgr.Length),
                ImageBufferPixelFormat.Bgr8,
                _targetWidth,
                _targetHeight,
                _targetWidth * 3);
            VideoScalerResult result = _videoScaler.ScaleImageBuffer(
                inputBuffer,
                outputBuffer,
                new VideoScalerOptions());
            if (result.Status != VideoScalerStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Video Super Resolution failed to scale a frame: {result.Status}.");
            }

            outputBuffer.CopyToByteArray(targetBgr);
            return CreateBuffer(ConvertBgrToBgra(targetBgr));
        }

        private static TimeSpan CalculateFrameDuration(uint numerator, uint denominator)
        {
            if (numerator == 0)
            {
                return TimeSpan.FromSeconds(1d / 30);
            }

            return TimeSpan.FromSeconds(Math.Max(1, denominator) / (double)numerator);
        }

        private static byte[] ConvertBgraToBgr(byte[] bgra)
        {
            var bgr = new byte[bgra.Length / 4 * 3];
            for (int sourceIndex = 0, targetIndex = 0;
                sourceIndex + 3 < bgra.Length;
                sourceIndex += 4, targetIndex += 3)
            {
                bgr[targetIndex] = bgra[sourceIndex];
                bgr[targetIndex + 1] = bgra[sourceIndex + 1];
                bgr[targetIndex + 2] = bgra[sourceIndex + 2];
            }

            return bgr;
        }

        private static byte[] ConvertBgrToBgra(byte[] bgr)
        {
            var bgra = new byte[bgr.Length / 3 * 4];
            for (int sourceIndex = 0, targetIndex = 0;
                sourceIndex + 2 < bgr.Length;
                sourceIndex += 3, targetIndex += 4)
            {
                bgra[targetIndex] = bgr[sourceIndex];
                bgra[targetIndex + 1] = bgr[sourceIndex + 1];
                bgra[targetIndex + 2] = bgr[sourceIndex + 2];
                bgra[targetIndex + 3] = byte.MaxValue;
            }

            return bgra;
        }

        private static IBuffer CreateBuffer(byte[] bytes)
        {
            using var writer = new DataWriter();
            writer.WriteBytes(bytes);
            return writer.DetachBuffer();
        }

        private static IBuffer CreateWritableBuffer(int byteCount)
        {
            return new global::Windows.Storage.Streams.Buffer((uint)byteCount)
            {
                Length = (uint)byteCount
            };
        }

        private static TimeSpan Min(TimeSpan left, TimeSpan right)
        {
            return left <= right ? left : right;
        }
    }
}
