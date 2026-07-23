using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.FileSystem;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsImageObjectEraseService : IImageObjectEraseService
{
    private const string EditCacheFolderName = "ImageEdit";

    private readonly IStorageService _storageService;

    public WindowsImageObjectEraseService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public ObjectEraseReadyState GetReadyState()
    {
        try
        {
            return MapReadyStates(
                ImageObjectExtractor.GetReadyState(),
                ImageObjectRemover.GetReadyState());
        }
        catch
        {
            return ObjectEraseReadyState.Unknown;
        }
    }

    public async Task<ObjectErasePreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ObjectEraseReadyState readyState = GetReadyState();
        if (readyState == ObjectEraseReadyState.Ready)
        {
            return ObjectErasePreparationResult.Success;
        }

        if (readyState is ObjectEraseReadyState.NotSupported or ObjectEraseReadyState.Disabled)
        {
            return ObjectErasePreparationResult.NotSupported;
        }

        try
        {
            if (ImageObjectExtractor.GetReadyState() != AIFeatureReadyState.Ready)
            {
                AIFeatureReadyResult extractorResult = await ImageObjectExtractor
                    .EnsureReadyAsync()
                    .AsTask(cancellationToken);
                if (extractorResult.Status != AIFeatureReadyResultState.Success)
                {
                    return ObjectErasePreparationResult.Failed(GetErrorMessage(extractorResult));
                }
            }

            if (ImageObjectRemover.GetReadyState() != AIFeatureReadyState.Ready)
            {
                AIFeatureReadyResult removerResult = await ImageObjectRemover
                    .EnsureReadyAsync()
                    .AsTask(cancellationToken);
                if (removerResult.Status != AIFeatureReadyResultState.Success)
                {
                    return ObjectErasePreparationResult.Failed(GetErrorMessage(removerResult));
                }
            }

            return GetReadyState() == ObjectEraseReadyState.Ready
                ? ObjectErasePreparationResult.Success
                : ObjectErasePreparationResult.Failed();
        }
        catch (OperationCanceledException)
        {
            return ObjectErasePreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ObjectErasePreparationResult.Failed(ex.Message);
        }
    }

    public async Task<ObjectEraseResult> EraseAsync(
        ObjectEraseRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetReadyState() != ObjectEraseReadyState.Ready)
        {
            return ObjectEraseResult.NotReady;
        }

        try
        {
            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(request.SourceImage.FilePath);
            using IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
            using SoftwareBitmap sourceBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight);

            cancellationToken.ThrowIfCancellationRequested();

            using ImageObjectExtractor extractor = await ImageObjectExtractor
                .CreateWithSoftwareBitmapAsync(sourceBitmap)
                .AsTask(cancellationToken);

            PointInt32 objectPoint = ScaleObjectPoint(
                request.ObjectPoint,
                request.SourceSize,
                sourceBitmap.PixelWidth,
                sourceBitmap.PixelHeight);
            var hint = new ImageObjectExtractorHint(
                includeRects: null,
                includePoints: [objectPoint],
                excludePoints: null);

            using SoftwareBitmap mask = await Task.Run(
                () => extractor.GetSoftwareBitmapObjectMask(hint),
                CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();

            using ImageObjectRemover remover = await ImageObjectRemover
                .CreateAsync()
                .AsTask(cancellationToken);
            using SoftwareBitmap resultBitmap = await Task.Run(
                () => remover.RemoveFromSoftwareBitmap(sourceBitmap, mask),
                CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();

            StorageFolder temporaryFolder = await StorageFolder.GetFolderFromPathAsync(
                _storageService.GetApplicationTemporaryFolderPath());
            StorageFolder outputFolder = await temporaryFolder.CreateFolderAsync(
                EditCacheFolderName,
                CreationCollisionOption.OpenIfExists);
            StorageFile outputFile = await outputFolder.CreateFileAsync(
                GetOutputFileName(request.SourceImage.FilePath),
                CreationCollisionOption.GenerateUniqueName);
            await SaveBitmapAsync(resultBitmap, outputFile, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return ObjectEraseResult.Success(new ImageFile(outputFile.Path));
        }
        catch (OperationCanceledException)
        {
            return ObjectEraseResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ObjectEraseResult.Failed(ex.Message);
        }
    }

    internal static ObjectEraseReadyState MapReadyStates(
        AIFeatureReadyState extractorState,
        AIFeatureReadyState removerState)
    {
        if (extractorState == AIFeatureReadyState.NotSupportedOnCurrentSystem ||
            removerState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
        {
            return ObjectEraseReadyState.NotSupported;
        }

        if (extractorState == AIFeatureReadyState.DisabledByUser ||
            removerState == AIFeatureReadyState.DisabledByUser)
        {
            return ObjectEraseReadyState.Disabled;
        }

        if (extractorState == AIFeatureReadyState.Ready &&
            removerState == AIFeatureReadyState.Ready)
        {
            return ObjectEraseReadyState.Ready;
        }

        if ((extractorState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady) &&
            (removerState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady))
        {
            return ObjectEraseReadyState.PreparationNeeded;
        }

        return ObjectEraseReadyState.Unknown;
    }

    internal static PointInt32 ScaleObjectPoint(
        Point point,
        Size sourceSize,
        int pixelWidth,
        int pixelHeight)
    {
        double scaleX = sourceSize.Width > 0 ? (double)pixelWidth / sourceSize.Width : 1;
        double scaleY = sourceSize.Height > 0 ? (double)pixelHeight / sourceSize.Height : 1;

        return new PointInt32(
            Math.Clamp((int)Math.Round(point.X * scaleX), 0, Math.Max(0, pixelWidth - 1)),
            Math.Clamp((int)Math.Round(point.Y * scaleY), 0, Math.Max(0, pixelHeight - 1)));
    }

    internal static string GetOutputFileName(string sourceFilePath)
    {
        return $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.object-erased.png";
    }

    private static async Task SaveBitmapAsync(
        SoftwareBitmap bitmap,
        StorageFile outputFile,
        CancellationToken cancellationToken)
    {
        using IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync().AsTask(cancellationToken);
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }
}
