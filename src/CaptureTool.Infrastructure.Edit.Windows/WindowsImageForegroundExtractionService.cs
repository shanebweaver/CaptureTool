using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
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

public sealed class WindowsImageForegroundExtractionService : IImageForegroundExtractionService
{
    private readonly IScratchArtifactStore _scratchArtifactStore;

    public WindowsImageForegroundExtractionService(IScratchArtifactStore scratchArtifactStore)
    {
        _scratchArtifactStore = scratchArtifactStore;
    }

    public ForegroundExtractionReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(ImageObjectExtractor.GetReadyState());
        }
        catch
        {
            return ForegroundExtractionReadyState.Unknown;
        }
    }

    public async Task<ForegroundExtractionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ForegroundExtractionReadyState readyState = GetReadyState();
        if (readyState == ForegroundExtractionReadyState.Ready)
        {
            return ForegroundExtractionPreparationResult.Success;
        }

        if (readyState is ForegroundExtractionReadyState.NotSupported or ForegroundExtractionReadyState.Disabled)
        {
            return ForegroundExtractionPreparationResult.NotSupported;
        }

        try
        {
            AIFeatureReadyResult result = await ImageObjectExtractor
                .EnsureReadyAsync()
                .AsTask(cancellationToken);

            return result.Status == AIFeatureReadyResultState.Success
                ? ForegroundExtractionPreparationResult.Success
                : ForegroundExtractionPreparationResult.Failed(GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return ForegroundExtractionPreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return ForegroundExtractionPreparationResult.Failed(ex.Message);
        }
    }

    public async Task<ForegroundExtractionResult> ExtractAsync(
        ForegroundExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetReadyState() != ForegroundExtractionReadyState.Ready)
        {
            return ForegroundExtractionResult.NotReady;
        }

        string? outputPath = null;
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

            PointInt32 foregroundPoint = ScaleForegroundPoint(
                request.ForegroundPoint,
                request.SourceSize,
                sourceBitmap.PixelWidth,
                sourceBitmap.PixelHeight);
            var hint = new ImageObjectExtractorHint(
                includeRects: null,
                includePoints: [foregroundPoint],
                excludePoints: null);

            using SoftwareBitmap mask = await Task.Run(
                () => extractor.GetSoftwareBitmapObjectMask(hint),
                CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();

            byte[] sourcePixels = await GetSourcePixelsAsync(decoder, cancellationToken);
            byte[] maskPixels = CopyPixels(mask, mask.PixelWidth * mask.PixelHeight);
            ApplyMaskToAlpha(sourcePixels, maskPixels);

            outputPath = _scratchArtifactStore.CreateLeasedArtifactPath("image-foreground-extraction", ".png");
            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outputPath));
            StorageFile outputFile = await outputFolder.CreateFileAsync(
                Path.GetFileName(outputPath),
                CreationCollisionOption.ReplaceExisting);
            await SavePixelsAsync(
                sourcePixels,
                sourceBitmap.PixelWidth,
                sourceBitmap.PixelHeight,
                outputFile,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return ForegroundExtractionResult.Success(new ImageFile(outputFile.Path));
        }
        catch (OperationCanceledException)
        {
            _scratchArtifactStore.DeleteArtifact(outputPath ?? string.Empty);
            return ForegroundExtractionResult.Cancelled;
        }
        catch (Exception ex)
        {
            _scratchArtifactStore.DeleteArtifact(outputPath ?? string.Empty);
            return ForegroundExtractionResult.Failed(ex.Message);
        }
    }

    internal static PointInt32 ScaleForegroundPoint(
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

    internal static void ApplyMaskToAlpha(byte[] bgraPixels, byte[] maskPixels)
    {
        int pixelCount = Math.Min(bgraPixels.Length / 4, maskPixels.Length);
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            int alphaIndex = (pixelIndex * 4) + 3;
            bgraPixels[alphaIndex] = (byte)((bgraPixels[alphaIndex] * maskPixels[pixelIndex]) / byte.MaxValue);
        }
    }

    internal static string GetOutputFileName(string sourceFilePath)
    {
        return $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.foreground.png";
    }

    internal static ForegroundExtractionReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => ForegroundExtractionReadyState.Ready,
            AIFeatureReadyState.NotReady => ForegroundExtractionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem => ForegroundExtractionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => ForegroundExtractionReadyState.Disabled,
            _ => ForegroundExtractionReadyState.Unknown
        };
    }

    private static async Task<byte[]> GetSourcePixelsAsync(
        BitmapDecoder decoder,
        CancellationToken cancellationToken)
    {
        PixelDataProvider pixelData = await decoder
            .GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage)
            .AsTask(cancellationToken);
        return pixelData.DetachPixelData();
    }

    private static byte[] CopyPixels(SoftwareBitmap bitmap, int byteCount)
    {
        var buffer = new global::Windows.Storage.Streams.Buffer((uint)byteCount);
        bitmap.CopyToBuffer(buffer);
        var bytes = new byte[byteCount];
        using DataReader reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static async Task SavePixelsAsync(
        byte[] pixels,
        int width,
        int height,
        StorageFile outputFile,
        CancellationToken cancellationToken)
    {
        using IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            (uint)width,
            (uint)height,
            96,
            96,
            pixels);
        await encoder.FlushAsync().AsTask(cancellationToken);
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }
}
