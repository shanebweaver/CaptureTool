using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.FileSystem;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsImageSuperResolutionService : IImageSuperResolutionService
{
    private const double RequestedScaleFactor = 2.0;
    private const int DocumentedMaxScaleFactor = 8;
    private const long MaxOutputPixels = 64L * 1024L * 1024L;

    private readonly IStorageService _storageService;

    public WindowsImageSuperResolutionService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public ImageSuperResolutionReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(ImageScaler.GetReadyState());
        }
        catch
        {
            return ImageSuperResolutionReadyState.Unknown;
        }
    }

    public async Task<ImageSuperResolutionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ImageSuperResolutionReadyState readyState = GetReadyState();
        if (readyState == ImageSuperResolutionReadyState.Ready)
        {
            return ImageSuperResolutionPreparationResult.Success;
        }

        if (readyState is ImageSuperResolutionReadyState.NotSupported or ImageSuperResolutionReadyState.Disabled)
        {
            return ImageSuperResolutionPreparationResult.NotSupported;
        }

        try
        {
            AIFeatureReadyResult result = await ImageScaler.EnsureReadyAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                return ImageSuperResolutionPreparationResult.Cancelled;
            }

            return result.Status == AIFeatureReadyResultState.Success
                ? ImageSuperResolutionPreparationResult.Success
                : ImageSuperResolutionPreparationResult.Failed(GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return ImageSuperResolutionPreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ImageSuperResolutionPreparationResult.Failed(ex.Message);
        }
    }

    public async Task<ImageSuperResolutionResult> GenerateAsync(
        ImageSuperResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetReadyState() != ImageSuperResolutionReadyState.Ready)
        {
            return ImageSuperResolutionResult.NotReady;
        }

        try
        {
            using ImageScaler imageScaler = await ImageScaler.CreateAsync();
            Size targetSize = CalculateTargetSize(
                request.SourceSize,
                request.ScaleFactor <= 0 ? RequestedScaleFactor : request.ScaleFactor,
                imageScaler.MaxSupportedScaleFactor);

            if (!IsOutputSizeAllowed(targetSize))
            {
                return ImageSuperResolutionResult.TooLarge("The super-resolution image would be too large to process safely.");
            }

            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(request.SourceImage.FilePath);
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(sourceFile);

            cancellationToken.ThrowIfCancellationRequested();

            using SoftwareBitmap scaledBitmap = imageScaler.ScaleSoftwareBitmap(
                sourceBitmap,
                targetSize.Width,
                targetSize.Height);

            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(_storageService.GetApplicationTemporaryFolderPath());
            string outputFileName = $"{Path.GetFileNameWithoutExtension(request.SourceImage.FilePath)}.super-{Guid.NewGuid():N}.png";
            StorageFile outputFile = await outputFolder.CreateFileAsync(outputFileName, CreationCollisionOption.ReplaceExisting);

            await SaveSoftwareBitmapAsync(scaledBitmap, outputFile);

            return ImageSuperResolutionResult.Success(new ImageFile(outputFile.Path), targetSize);
        }
        catch (OperationCanceledException)
        {
            return ImageSuperResolutionResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ImageSuperResolutionResult.Failed(ex.Message);
        }
    }

    internal static Size CalculateTargetSize(Size sourceSize, double requestedScaleFactor, int scalerMaxSupportedScaleFactor)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return Size.Empty;
        }

        double scaleFactor = Math.Min(
            Math.Min(requestedScaleFactor, DocumentedMaxScaleFactor),
            Math.Max(1, scalerMaxSupportedScaleFactor));

        return new(
            Math.Max(1, (int)Math.Round(sourceSize.Width * scaleFactor)),
            Math.Max(1, (int)Math.Round(sourceSize.Height * scaleFactor)));
    }

    private static async Task<SoftwareBitmap> LoadSoftwareBitmapAsync(StorageFile sourceFile)
    {
        using var stream = await sourceFile.OpenAsync(FileAccessMode.Read);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static async Task SaveSoftwareBitmapAsync(SoftwareBitmap bitmap, StorageFile outputFile)
    {
        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
    }

    private static bool IsOutputSizeAllowed(Size targetSize)
    {
        return targetSize.Width > 0 &&
            targetSize.Height > 0 &&
            (long)targetSize.Width * targetSize.Height <= MaxOutputPixels;
    }

    private static ImageSuperResolutionReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => ImageSuperResolutionReadyState.Ready,
            AIFeatureReadyState.NotReady => ImageSuperResolutionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem => ImageSuperResolutionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => ImageSuperResolutionReadyState.Disabled,
            _ => ImageSuperResolutionReadyState.Unknown
        };
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }
}
