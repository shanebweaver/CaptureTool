using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsImageMediaPropertiesAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldDeclareLightweightOnDeviceObservation()
    {
        var analyzer = new WindowsImageMediaPropertiesAnalyzer();

        Assert.AreEqual(AnalysisCapabilities.MediaPropertiesV1, analyzer.Descriptor.Capability);
        Assert.AreEqual(ProcessingBoundary.OnDevice, analyzer.Descriptor.ProcessingBoundary);
        Assert.AreEqual(CaptureAnalyzerDataKind.None, analyzer.Descriptor.DataSent);
        Assert.AreEqual(CaptureAnalyzerWorkloadClass.Lightweight, analyzer.Descriptor.WorkloadClass);
        CollectionAssert.AreEqual(
            new[] { CaptureMediaKind.Image },
            analyzer.Descriptor.SupportedMediaKinds.ToArray());
    }

    [TestMethod]
    public async Task AnalyzePng_ShouldReturnCanonicalPixelDimensions()
    {
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nGQAAAAASUVORK5CYII=");
        var analyzer = new WindowsImageMediaPropertiesAnalyzer();
        var source = new MemoryAnalysisSource(png);
        var policy = AnalysisProcessingPolicy.LocalOnly(Purpose);
        var request = new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            policy,
            source);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(request);

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Succeeded, output.Status);
        var payload = (MediaPropertiesV1)output.Payload!;
        Assert.AreEqual(new PixelSize(1, 1), payload.PixelSize);
    }

    private sealed class MemoryAnalysisSource : ICaptureAnalysisSource
    {
        private readonly byte[] _bytes;

        public MemoryAnalysisSource(byte[] bytes)
        {
            _bytes = bytes;
            DateTimeOffset timestamp = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
            SourceRevision = new(
                bytes.LongLength,
                timestamp,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(_bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
