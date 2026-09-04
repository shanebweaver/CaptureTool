using CaptureTool.Application.Abstractions.Edit.Image.Description;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsImageDescriptionService :
    IImageDescriptionService,
    IImageDescriptionAnalysisService
{
    private static readonly string? RuntimeVersion =
        typeof(ImageDescriptionGenerator).Assembly.GetName().Version?.ToString();

    private static readonly ImageDescriptionModelDescriptor Descriptor = new(
        ProducerId: "microsoft-windows",
        ModelId: "windows-app-sdk-image-description",
        ModelVersion: null,
        RuntimeId: "windows-app-sdk-ai",
        RuntimeVersion,
        PackageVersion: RuntimeVersion);

    public ImageDescriptionModelDescriptor ModelDescriptor => Descriptor;

    public ImageDescriptionReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(ImageDescriptionGenerator.GetReadyState());
        }
        catch
        {
            return ImageDescriptionReadyState.Unknown;
        }
    }

    public async Task<ImageDescriptionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        WindowsImageDescriptionPreparationAttempt attempt = await PrepareCoreAsync(
            progress: null,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result.Status switch
        {
            ImageDescriptionAnalysisPreparationStatus.Succeeded =>
                ImageDescriptionPreparationResult.Success,
            ImageDescriptionAnalysisPreparationStatus.Cancelled =>
                ImageDescriptionPreparationResult.Cancelled,
            ImageDescriptionAnalysisPreparationStatus.Unsupported or
                ImageDescriptionAnalysisPreparationStatus.Disabled =>
                    ImageDescriptionPreparationResult.NotSupported,
            _ => ImageDescriptionPreparationResult.Failed(attempt.ErrorMessage),
        };
    }

    public async Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        WindowsImageDescriptionPreparationAttempt attempt = await PrepareCoreAsync(
            progress,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result;
    }

    public async Task<CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult> DescribeAsync(
        ImageDescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        WindowsImageDescriptionAttempt attempt = await DescribeCoreAsync(
            request.SourceImage,
            request.Mode,
            cancellationToken).ConfigureAwait(false);
        return ToInteractiveResult(attempt);
    }

    public async Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
        Stream sourceImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceImage);
        cancellationToken.ThrowIfCancellationRequested();

        WindowsImageDescriptionAttempt attempt = await DescribeCoreAsync(
            sourceImage,
            ImageDescriptionMode.Brief,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result;
    }

    internal static ImageDescriptionReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => ImageDescriptionReadyState.Ready,
            AIFeatureReadyState.NotReady => ImageDescriptionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem => ImageDescriptionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => ImageDescriptionReadyState.Disabled,
            _ => ImageDescriptionReadyState.Unknown
        };
    }

    internal static ImageDescriptionKind MapMode(ImageDescriptionMode mode)
    {
        return mode switch
        {
            ImageDescriptionMode.Brief => ImageDescriptionKind.BriefDescription,
            ImageDescriptionMode.Detailed => ImageDescriptionKind.DetailedDescription,
            ImageDescriptionMode.Diagram => ImageDescriptionKind.DiagramDescription,
            ImageDescriptionMode.Accessible => ImageDescriptionKind.AccessibleDescription,
            _ => ImageDescriptionKind.BriefDescription
        };
    }

    private async Task<WindowsImageDescriptionPreparationAttempt> PrepareCoreAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ImageDescriptionReadyState readyState = GetReadyState();
        if (readyState == ImageDescriptionReadyState.Ready)
        {
            progress?.Report(1);
            return WindowsImageDescriptionPreparationAttempt.Succeeded;
        }

        if (readyState == ImageDescriptionReadyState.NotSupported)
        {
            return WindowsImageDescriptionPreparationAttempt.Unsupported;
        }

        if (readyState == ImageDescriptionReadyState.Disabled)
        {
            return WindowsImageDescriptionPreparationAttempt.Disabled;
        }

        try
        {
            progress?.Report(0);
            AIFeatureReadyResult result = await ImageDescriptionGenerator
                .EnsureReadyAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return WindowsImageDescriptionPreparationAttempt.Cancelled;
            }

            if (result.Status == AIFeatureReadyResultState.Success)
            {
                progress?.Report(1);
                return WindowsImageDescriptionPreparationAttempt.Succeeded;
            }

            return WindowsImageDescriptionPreparationAttempt.TransientFailure(
                GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return WindowsImageDescriptionPreparationAttempt.Cancelled;
        }
        catch (COMException exception)
        {
            return WindowsImageDescriptionPreparationAttempt.TransientFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return WindowsImageDescriptionPreparationAttempt.TerminalFailure(exception.Message);
        }
    }

    private async Task<WindowsImageDescriptionAttempt> DescribeCoreAsync(
        Stream sourceImage,
        ImageDescriptionMode mode,
        CancellationToken cancellationToken)
    {
        ImageDescriptionReadyState readyState = GetReadyState();
        if (readyState != ImageDescriptionReadyState.Ready)
        {
            return readyState switch
            {
                ImageDescriptionReadyState.PreparationNeeded =>
                    WindowsImageDescriptionAttempt.PreparationRequired,
                ImageDescriptionReadyState.NotSupported =>
                    WindowsImageDescriptionAttempt.Unsupported,
                ImageDescriptionReadyState.Disabled =>
                    WindowsImageDescriptionAttempt.Disabled,
                _ => WindowsImageDescriptionAttempt.TransientFailure(),
            };
        }

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(sourceImage)
                .ConfigureAwait(false);
            using ImageBuffer inputImage = ImageBuffer.CreateForSoftwareBitmap(sourceBitmap);
            using ImageDescriptionGenerator generator = await ImageDescriptionGenerator
                .CreateAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            Microsoft.Windows.AI.Imaging.ImageDescriptionResult result = await generator
                .DescribeAsync(
                    inputImage,
                    MapMode(mode),
                    new ContentFilterOptions())
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return WindowsImageDescriptionAttempt.Cancelled;
            }

            return MapResult(result);
        }
        catch (OperationCanceledException)
        {
            return WindowsImageDescriptionAttempt.Cancelled;
        }
        catch (COMException exception)
        {
            return WindowsImageDescriptionAttempt.TransientFailure(exception.Message);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return WindowsImageDescriptionAttempt.TerminalFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return WindowsImageDescriptionAttempt.TransientFailure(exception.Message);
        }
    }

    private static WindowsImageDescriptionAttempt MapResult(
        Microsoft.Windows.AI.Imaging.ImageDescriptionResult result)
    {
        return result.Status switch
        {
            ImageDescriptionResultStatus.Complete when !string.IsNullOrWhiteSpace(result.Description) =>
                WindowsImageDescriptionAttempt.Succeeded(result.Description),
            ImageDescriptionResultStatus.BlockedByPolicy =>
                WindowsImageDescriptionAttempt.BlockedByPolicy,
            ImageDescriptionResultStatus.ImageBlockedByContentModeration or
            ImageDescriptionResultStatus.TextInImageBlockedByContentModeration or
            ImageDescriptionResultStatus.DescriptionTextBlockedByContentModeration =>
                WindowsImageDescriptionAttempt.BlockedByContentSafety,
            ImageDescriptionResultStatus.ImageHasTooMuchText =>
                WindowsImageDescriptionAttempt.InputTooLarge,
            _ => WindowsImageDescriptionAttempt.TerminalFailure()
        };
    }

    private static CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult ToInteractiveResult(
        WindowsImageDescriptionAttempt attempt)
    {
        return attempt.Result.Status switch
        {
            ImageDescriptionAnalysisStatus.Succeeded =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Success(
                    attempt.Result.Description),
            ImageDescriptionAnalysisStatus.Cancelled =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Cancelled,
            ImageDescriptionAnalysisStatus.BlockedByPolicy =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.BlockedByPolicy,
            ImageDescriptionAnalysisStatus.BlockedByContentSafety =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.BlockedByContentSafety,
            ImageDescriptionAnalysisStatus.InputTooLarge =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.TooMuchText,
            ImageDescriptionAnalysisStatus.PreparationRequired or
            ImageDescriptionAnalysisStatus.Unsupported or
            ImageDescriptionAnalysisStatus.Disabled =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.NotReady,
            _ => CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Failed(
                attempt.ErrorMessage),
        };
    }

    private static async Task<SoftwareBitmap> LoadSoftwareBitmapAsync(Stream sourceStream)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        using IRandomAccessStream randomAccessStream = sourceStream.AsRandomAccessStream();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }

    private sealed record WindowsImageDescriptionAttempt(
        ImageDescriptionAnalysisResult Result,
        string? ErrorMessage)
    {
        public static WindowsImageDescriptionAttempt Succeeded(string description)
        {
            return new(ImageDescriptionAnalysisResult.Succeeded(description), null);
        }

        public static WindowsImageDescriptionAttempt PreparationRequired { get; } = new(
            ImageDescriptionAnalysisResult.PreparationRequired,
            null);

        public static WindowsImageDescriptionAttempt Unsupported { get; } = new(
            ImageDescriptionAnalysisResult.Unsupported,
            null);

        public static WindowsImageDescriptionAttempt Disabled { get; } = new(
            ImageDescriptionAnalysisResult.Disabled,
            null);

        public static WindowsImageDescriptionAttempt BlockedByPolicy { get; } = new(
            ImageDescriptionAnalysisResult.BlockedByPolicy,
            null);

        public static WindowsImageDescriptionAttempt BlockedByContentSafety { get; } = new(
            ImageDescriptionAnalysisResult.BlockedByContentSafety,
            null);

        public static WindowsImageDescriptionAttempt InputTooLarge { get; } = new(
            ImageDescriptionAnalysisResult.InputTooLarge,
            null);

        public static WindowsImageDescriptionAttempt Cancelled { get; } = new(
            ImageDescriptionAnalysisResult.Cancelled,
            null);

        public static WindowsImageDescriptionAttempt TransientFailure(string? errorMessage = null)
        {
            return new(ImageDescriptionAnalysisResult.TransientFailure, errorMessage);
        }

        public static WindowsImageDescriptionAttempt TerminalFailure(string? errorMessage = null)
        {
            return new(ImageDescriptionAnalysisResult.TerminalFailure, errorMessage);
        }
    }

    private sealed record WindowsImageDescriptionPreparationAttempt(
        ImageDescriptionAnalysisPreparationResult Result,
        string? ErrorMessage)
    {
        public static WindowsImageDescriptionPreparationAttempt Succeeded { get; } = new(
            ImageDescriptionAnalysisPreparationResult.Succeeded,
            null);

        public static WindowsImageDescriptionPreparationAttempt Unsupported { get; } = new(
            ImageDescriptionAnalysisPreparationResult.Unsupported,
            null);

        public static WindowsImageDescriptionPreparationAttempt Disabled { get; } = new(
            ImageDescriptionAnalysisPreparationResult.Disabled,
            null);

        public static WindowsImageDescriptionPreparationAttempt Cancelled { get; } = new(
            ImageDescriptionAnalysisPreparationResult.Cancelled,
            null);

        public static WindowsImageDescriptionPreparationAttempt TransientFailure(string? errorMessage)
        {
            return new(ImageDescriptionAnalysisPreparationResult.TransientFailure, errorMessage);
        }

        public static WindowsImageDescriptionPreparationAttempt TerminalFailure(string? errorMessage)
        {
            return new(ImageDescriptionAnalysisPreparationResult.TerminalFailure, errorMessage);
        }
    }
}
