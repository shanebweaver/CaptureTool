using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsImageMediaPropertiesAnalyzer : ICaptureAnalyzer
{
    private static readonly AnalyzerIdentity Identity = new(
        analyzerId: "windows-image-media-properties",
        providerId: "microsoft-windows",
        modelId: null,
        modelVersion: null,
        adapterVersion: "1.0.0",
        runtimeId: "windows-graphics-imaging",
        runtimeVersion: "1",
        packageVersion: null,
        configurationFingerprint: null);

    public CaptureAnalyzerDescriptor Descriptor { get; } = new(
        AnalysisCapabilities.MediaPropertiesV1,
        Identity,
        [CaptureMediaKind.Image],
        ProcessingBoundary.OnDevice,
        CaptureAnalyzerDataKind.None,
        CaptureAnalyzerRequirement.OperatingSystemCapability,
        CaptureAnalyzerWorkloadClass.Lightweight,
        maximumSourceBytes: null,
        qualityTier: 100);

    public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
        CaptureAnalyzerAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The availability request targets another analyzer.", nameof(request));
        }

        return ValueTask.FromResult(CaptureAnalyzerAvailability.Available);
    }

    public async Task<CaptureAnalyzerOutput> AnalyzeAsync(
        CaptureAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The analysis request targets another analyzer.", nameof(request));
        }

        try
        {
            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);
            if (source.CanSeek)
            {
                source.Position = 0;
            }

            using IRandomAccessStream randomAccessStream = source.AsRandomAccessStream();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            cancellationToken.ThrowIfCancellationRequested();
            if (decoder.PixelWidth > int.MaxValue || decoder.PixelHeight > int.MaxValue)
            {
                return CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.UnsupportedMedia,
                    AnalysisFailureDisposition.Terminal));
            }

            var payload = new MediaPropertiesV1(
                CaptureMediaKind.Image,
                new PixelSize((int)decoder.PixelWidth, (int)decoder.PixelHeight));
            return CaptureAnalyzerOutput.Succeeded(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CaptureAnalyzerOutput.Cancelled;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or COMException)
        {
            return CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.InvalidSource,
                AnalysisFailureDisposition.Terminal));
        }
    }
}
