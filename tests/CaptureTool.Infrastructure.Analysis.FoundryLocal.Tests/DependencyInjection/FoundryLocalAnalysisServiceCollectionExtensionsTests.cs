using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Storage;
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
            descriptor.ServiceType == typeof(IFoundryLocalSdkClient) &&
            descriptor.ImplementationType == typeof(FoundryLocalSdkClient) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFoundryLocalModelProvenanceStore) &&
            descriptor.ImplementationType == typeof(FoundryLocalModelProvenanceStore) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFoundryLocalSpeechLanguagePolicy) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.AreEqual(2, services.Count(descriptor =>
            descriptor.ServiceType == typeof(IFoundryLocalSpeechTranscriptionService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.AreEqual(2, services.Count(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(FoundryLocalWhisperSpeechTranscriptionService)));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(FoundryLocalNemotronSpeechTranscriptionService)));
    }

    [TestMethod]
    public void PackagedProviderManifest_ShouldMatchRegisteredAnalyzerContract()
    {
        var whisperAnalyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService(),
            videoAudioExtraction: null,
            FoundryLocalSpeechModelConfiguration.Whisper);
        var nemotronAnalyzer = new FoundryLocalSpeechTranscriptAnalyzer(
            new StubTranscriptionService(),
            videoAudioExtraction: null,
            FoundryLocalSpeechModelConfiguration.NemotronMultilingual);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "FoundryLocalCaptureAnalysisProviders.json")));
        JsonElement root = document.RootElement;
        JsonElement provider = root.GetProperty("providers").EnumerateArray().Single();
        Dictionary<string, JsonElement> manifestAnalyzers = provider
            .GetProperty("analyzers")
            .EnumerateArray()
            .ToDictionary(
                analyzer => analyzer.GetProperty("analyzerId").GetString()!,
                analyzer => analyzer);

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("on-device", root.GetProperty("processingBoundary").GetString());
        Assert.AreEqual(whisperAnalyzer.Descriptor.Identity.ProviderId,
            provider.GetProperty("providerId").GetString());
        foreach (FoundryLocalSpeechTranscriptAnalyzer analyzer in
            new[] { whisperAnalyzer, nemotronAnalyzer })
        {
            JsonElement manifestAnalyzer = manifestAnalyzers[
                analyzer.Descriptor.Identity.AnalyzerId];
            Assert.AreEqual(
                $"{analyzer.Descriptor.Capability.Id.Value}/v{analyzer.Descriptor.Capability.SchemaVersion.Value}",
                manifestAnalyzer.GetProperty("capability").GetString());
        }
    }

    [TestMethod]
    public void AddFoundryLocalAnalysisProvider_ShouldResolveBothModelsWithoutInitializingSdk()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationLocalCachePathProvider>(
            new StubPathProvider(Path.Combine(
                Path.GetTempPath(),
                "CaptureToolFoundryDiTests",
                Guid.NewGuid().ToString("N"))));
        services.AddSingleton<ILocalizationService>(new StubLocalizationService("fr-FR"));
        services.AddFoundryLocalAnalysisProvider();
        using ServiceProvider provider = services.BuildServiceProvider();

        ICaptureAnalyzer[] analyzers = provider.GetServices<ICaptureAnalyzer>().ToArray();
        IFoundryLocalSpeechTranscriptionService[] transcriptionServices = provider
            .GetServices<IFoundryLocalSpeechTranscriptionService>()
            .ToArray();

        Assert.HasCount(2, analyzers);
        Assert.HasCount(2, transcriptionServices);
        Assert.AreEqual("fr", transcriptionServices[^1].LanguageHint);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "foundry-local-speech-transcript",
                "foundry-local-nemotron-multilingual-speech-transcript",
            },
            analyzers.Select(analyzer => analyzer.Descriptor.Identity.AnalyzerId).ToArray());
    }

    private sealed class StubTranscriptionService : IFoundryLocalSpeechTranscriptionService
    {
        public FoundryLocalModelProvenance? ModelProvenance => null;

        public string LanguageHint => "en";

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

        public Task ReleaseModelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubPathProvider(string root) : IApplicationLocalCachePathProvider
    {
        public string GetApplicationLocalCacheFolderPath() => root;
    }

    private sealed class StubLocalizationService(string language) : ILocalizationService
    {
        private readonly IAppLanguage _language = new StubAppLanguage(language);

        public IAppLanguage? LanguageOverride => _language;

        public IAppLanguage? RequestedLanguage => _language;

        public IAppLanguage? StartupLanguage => _language;

        public IAppLanguage? DefaultLanguage => _language;

        public IAppLanguage[] SupportedLanguages => [_language];

        public void Initialize(string languageOverride)
        {
        }

        public string GetString(string resourceKey) => resourceKey;

        public void OverrideLanguage(IAppLanguage? language)
        {
        }
    }

    private sealed record StubAppLanguage(string Value) : IAppLanguage;
}
