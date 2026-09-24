using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.AI.Foundry.Local;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed class FoundryImageDescriptionAnalyzer(string id, string alias, FoundryRuntime runtime,
    WindowsAnalysisMedia media) : IMediaAnalyzer
{
    private IModel? _model;
    public MediaAnalyzerDescriptor Descriptor { get; } = new(id, AnalysisCapability.Description, [AnalysisMediaKind.Image, AnalysisMediaKind.Video]);

    public async ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (kind is not (AnalysisMediaKind.Image or AnalysisMediaKind.Video) || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) return AnalyzerAvailability.Unsupported;
        return _model != null && await _model.IsCachedAsync(ct).ConfigureAwait(false) ? AnalyzerAvailability.Ready : AnalyzerAvailability.PreparationRequired;
    }

    public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        _model = await runtime.ResolveAsync(alias, ct).ConfigureAwait(false);
        if (_model?.Info.Task != "vision-language-chat") { _model = null; return AnalyzerAvailability.Unsupported; }
        if (!await _model.IsCachedAsync(ct).ConfigureAwait(false))
            await _model.DownloadAsync(value => progress?.Report(new(AnalysisProgressStage.Preparing,
                double.IsFinite(value) ? Math.Clamp(value / 100d, 0, 1) : null)), ct).ConfigureAwait(false);
        progress?.Report(new(AnalysisProgressStage.Preparing, 1));
        return AnalyzerAvailability.Ready;
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        if (input.MediaKind is not (AnalysisMediaKind.Image or AnalysisMediaKind.Video))
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-media");
        IModel? model = _model;
        if (model == null) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.TemporarilyUnavailable, "model-not-prepared");
        bool serving = false;
        try
        {
            await model.LoadAsync(ct).ConfigureAwait(false);
            Uri endpoint = await runtime.StartVisionServiceAsync(ct).ConfigureAwait(false);
            serving = true;
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = FoundryVisionProtocol.MaximumResponseBytes };
            List<MediaDescription> descriptions = [];
            if (input.MediaKind == AnalysisMediaKind.Image)
            {
                using var bitmap = await WindowsAnalysisMedia.LoadImageAsync(input.SourcePath, ct).ConfigureAwait(false);
                var result = await DescribeAsync(bitmap).ConfigureAwait(false);
                if (result.Status != AnalyzerOutcomeKind.Succeeded) return AnalyzerOutcome.Unsuccessful(result.Status, "vision-response-rejected");
                descriptions.Add(new(result.Text!));
            }
            else
            {
                await foreach (var frame in media.ReadFramesAsync(input.SourcePath, 8, TimeSpan.FromSeconds(30), ct).ConfigureAwait(false))
                {
                    var result = await DescribeAsync(frame.Bitmap).ConfigureAwait(false);
                    if (result.Status != AnalyzerOutcomeKind.Succeeded) return AnalyzerOutcome.Unsuccessful(result.Status, "vision-response-rejected");
                    descriptions.Add(new(result.Text!, frame.Timestamp));
                    progress?.Report(new(AnalysisProgressStage.Analyzing, descriptions.Count / 8d));
                }
            }
            return AnalyzerOutcome.Success(new DescriptionMetadata(descriptions),
                new(id, "foundry-local", model.Id, "1", model.Info.Version.ToString(CultureInfo.InvariantCulture)));

            async Task<(AnalyzerOutcomeKind Status, string? Text)> DescribeAsync(SoftwareBitmap bitmap)
            {
                byte[] image = await EncodeAsync(bitmap, ct).ConfigureAwait(false);
                using var content = new ByteArrayContent(FoundryVisionProtocol.CreateRequest(model.Id, image));
                content.Headers.ContentType = new("application/json");
                ct.ThrowIfCancellationRequested();
                // Aborting HTTP does not prove native inference stopped. Keep this task and the
                // model alive until the response ends; the worker's timeout/cancellation fence
                // hides stale progress/results and prevents any overlapping native invocation.
                using var response = await http.PostAsync(endpoint, content, CancellationToken.None).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (!response.IsSuccessStatusCode) return (AnalyzerOutcomeKind.Failed, null);
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
                return FoundryVisionProtocol.ParseResponse(bytes, model.Id);
            }
        }
        catch (InvalidAnalysisMediaException) { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "invalid-media"); }
        finally
        {
            try { if (serving) await runtime.StopVisionServiceAsync().ConfigureAwait(false); }
            finally { await model.UnloadAsync(CancellationToken.None).ConfigureAwait(false); }
        }
    }

    private static async Task<byte[]> EncodeAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        using var buffer = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, buffer).AsTask(ct).ConfigureAwait(false);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
        if (buffer.Size > FoundryVisionProtocol.MaximumImageBytes) throw new InvalidDataException("Encoded image exceeds its bound.");
        buffer.Seek(0);
        using var reader = new DataReader(buffer);
        await reader.LoadAsync((uint)buffer.Size).AsTask(ct).ConfigureAwait(false);
        byte[] image = new byte[(int)buffer.Size];
        reader.ReadBytes(image);
        return image;
    }
}
