using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Infrastructure.Analysis.FoundryLocal.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests.DependencyInjection;

[TestClass]
public sealed class FoundryLocalAnalysisServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddFoundryLocalAnalysisProvider_ShouldRegisterSpeechServicesExplicitly()
    {
        var services = new ServiceCollection();

        services.AddFoundryLocalAnalysisProvider();

        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFoundryLocalSpeechTranscriptionService) &&
            descriptor.ImplementationType == typeof(FoundryLocalSpeechTranscriptionService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(FoundryLocalSpeechTranscriptAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }

    [TestMethod]
    public void PackagedProviderManifest_ShouldMatchRegisteredAnalyzerContract()
    {
        var analyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService());
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "FoundryLocalCaptureAnalysisProviders.json")));
        JsonElement root = document.RootElement;
        JsonElement provider = root.GetProperty("providers").EnumerateArray().Single();
        JsonElement manifestAnalyzer = provider.GetProperty("analyzers").EnumerateArray().Single();

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("on-device", root.GetProperty("processingBoundary").GetString());
        Assert.AreEqual(analyzer.Descriptor.Identity.ProviderId,
            provider.GetProperty("providerId").GetString());
        Assert.AreEqual(analyzer.Descriptor.Identity.AnalyzerId,
            manifestAnalyzer.GetProperty("analyzerId").GetString());
        Assert.AreEqual(
            $"{analyzer.Descriptor.Capability.Id.Value}/v{analyzer.Descriptor.Capability.SchemaVersion.Value}",
            manifestAnalyzer.GetProperty("capability").GetString());
    }

    private sealed class StubTranscriptionService : IFoundryLocalSpeechTranscriptionService
    {
        public FoundryLocalSpeechReadyState GetReadyState() =>
            FoundryLocalSpeechReadyState.NotSupported;

        public Task<FoundryLocalSpeechPreparationResult> PrepareAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FoundryLocalSpeechPreparationResult(
                FoundryLocalSpeechPreparationStatus.Unsupported));

        public Task<FoundryLocalTranscriptionResult> TranscribeAsync(
            Stream audio,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FoundryLocalTranscriptionResult(
                FoundryLocalTranscriptionStatus.Unsupported));
    }
}
