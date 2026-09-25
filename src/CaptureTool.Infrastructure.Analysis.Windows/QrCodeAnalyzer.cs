using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using CaptureTool.Infrastructure.Media;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows;

internal sealed class QrCodeAnalyzer(string id, AnalysisMediaKind mediaKind, WindowsAnalysisMedia media) : IMediaAnalyzer
{
    private const int MaximumCodes = 50000;
    public MediaAnalyzerDescriptor Descriptor { get; } = new(id, AnalysisCapability.QrCodeDetection, [mediaKind]);

    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(kind == mediaKind ? AnalyzerAvailability.Ready : AnalyzerAvailability.Unsupported);
    }

    public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(AnalyzerAvailability.Ready);
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (input.MediaKind != mediaKind) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-media");
        try
        {
            List<DecodedQrCode> codes = [];
            if (mediaKind == AnalysisMediaKind.Image)
            {
                using SoftwareBitmap bitmap = await WindowsAnalysisMedia.LoadImageAsync(input.SourcePath, ct).ConfigureAwait(false);
                codes.AddRange(Decode(bitmap, null, ct));
            }
            else
            {
                const int maximumFrames = 64;
                int count = 0;
                await foreach (var frame in media.ReadFramesAsync(input.SourcePath, maximumFrames, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
                {
                    codes.AddRange(Decode(frame.Bitmap, frame.Timestamp, ct));
                    if (codes.Count > MaximumCodes) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "output-limit");
                    progress?.Report(new(AnalysisProgressStage.Analyzing, ++count / (double)maximumFrames));
                }
            }
            ct.ThrowIfCancellationRequested();
            if (codes.Count > MaximumCodes) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "output-limit");
            return AnalyzerOutcome.Success(new QrCodeMetadata(codes),
                new(id, "zxing", "qr-code-decoder", "1", typeof(ZXing.BarcodeReaderGeneric).Assembly.GetName().Version?.ToString()));
        }
        catch (InvalidAnalysisMediaException) { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "invalid-media"); }
    }

    private static IEnumerable<DecodedQrCode> Decode(SoftwareBitmap bitmap, TimeSpan? timestamp, CancellationToken ct)
    {
        int byteCount = checked(bitmap.PixelWidth * bitmap.PixelHeight * 4);
        var buffer = new global::Windows.Storage.Streams.Buffer((uint)byteCount);
        bitmap.CopyToBuffer(buffer);
        byte[] pixels = new byte[byteCount];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(pixels);
        return QrCodeDecoder.Decode(pixels, bitmap.PixelWidth, bitmap.PixelHeight, ct)
            .Select(code => new DecodedQrCode(code.Value, code.Bounds, timestamp));
    }
}
