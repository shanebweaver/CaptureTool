using CaptureTool.Application.Abstractions.Edit.Image.Description;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsImageDescriptionService : IImageDescriptionService
{
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
        cancellationToken.ThrowIfCancellationRequested();

        ImageDescriptionReadyState readyState = GetReadyState();
        if (readyState == ImageDescriptionReadyState.Ready)
        {
            return ImageDescriptionPreparationResult.Success;
        }

        if (readyState is ImageDescriptionReadyState.NotSupported or ImageDescriptionReadyState.Disabled)
        {
            return ImageDescriptionPreparationResult.NotSupported;
        }

        try
        {
            AIFeatureReadyResult result = await ImageDescriptionGenerator.EnsureReadyAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                return ImageDescriptionPreparationResult.Cancelled;
            }

            return result.Status == AIFeatureReadyResultState.Success
                ? ImageDescriptionPreparationResult.Success
                : ImageDescriptionPreparationResult.Failed(GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return ImageDescriptionPreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ImageDescriptionPreparationResult.Failed(ex.Message);
        }
    }

    public async Task<CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult> DescribeAsync(
        ImageDescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetReadyState() != ImageDescriptionReadyState.Ready)
        {
            return CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.NotReady;
        }

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(request.SourceImage);
            using ImageBuffer inputImage = ImageBuffer.CreateForSoftwareBitmap(sourceBitmap);
            using ImageDescriptionGenerator generator = await ImageDescriptionGenerator.CreateAsync();

            cancellationToken.ThrowIfCancellationRequested();

            Microsoft.Windows.AI.Imaging.ImageDescriptionResult result = await generator.DescribeAsync(
                inputImage,
                MapMode(request.Mode),
                new ContentFilterOptions());

            if (cancellationToken.IsCancellationRequested)
            {
                return CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Cancelled;
            }

            return MapResult(result);
        }
        catch (OperationCanceledException)
        {
            return CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Cancelled;
        }
        catch (Exception ex)
        {
            return CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Failed(ex.Message);
        }
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

    private static CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult MapResult(
        Microsoft.Windows.AI.Imaging.ImageDescriptionResult result)
    {
        return result.Status switch
        {
            ImageDescriptionResultStatus.Complete when !string.IsNullOrWhiteSpace(result.Description) =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Success(result.Description),
            ImageDescriptionResultStatus.BlockedByPolicy => CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.BlockedByPolicy,
            ImageDescriptionResultStatus.ImageBlockedByContentModeration or
            ImageDescriptionResultStatus.TextInImageBlockedByContentModeration or
            ImageDescriptionResultStatus.DescriptionTextBlockedByContentModeration =>
                CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.BlockedByContentSafety,
            ImageDescriptionResultStatus.ImageHasTooMuchText => CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.TooMuchText,
            _ => CaptureTool.Application.Abstractions.Edit.Image.Description.ImageDescriptionResult.Failed()
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
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }
}
