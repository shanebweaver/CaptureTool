using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class FoundryLocalSpeechTranscriptionServiceTests
{
    private static readonly FoundryLocalModelProvenance GpuProvenance = new(
        "whisper-tiny",
        "whisper-tiny-winml-gpu-v4",
        "4",
        "GPU",
        "WinMLExecutionProvider",
        $"sha256:{new string('a', 64)}");

    [TestMethod]
    public void GetReadyState_DoesNotInitializeOrAcquireAnything()
    {
        var sdk = new FakeSdkClient();
        using var service = CreateService(sdk);

        FoundryLocalSpeechReadyState state = service.GetReadyState();

        Assert.AreEqual(FoundryLocalSpeechReadyState.PreparationNeeded, state);
        Assert.AreEqual(0, sdk.InitializeCalls);
        Assert.AreEqual(0, sdk.DownloadExecutionProviderCalls);
        Assert.AreEqual(0, sdk.GetModelCalls);
    }

    [TestMethod]
    public void Constructor_RestoresLastResolvedProvenanceWithoutLoadingTheModel()
    {
        var sdk = new FakeSdkClient();
        using var service = CreateService(sdk, GpuProvenance);

        Assert.AreEqual(GpuProvenance, service.ModelProvenance);
        Assert.AreEqual(FoundryLocalSpeechReadyState.PreparationNeeded, service.GetReadyState());
        Assert.AreEqual(0, sdk.InitializeCalls);
        Assert.AreEqual(0, sdk.GetModelCalls);
    }

    [TestMethod]
    public async Task PrepareAsync_DownloadsExecutionProvidersAndSelectedModelExplicitly()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = false };
        var sdk = new FakeSdkClient
        {
            Providers =
            [
                new FoundryLocalExecutionProvider("WinMLExecutionProvider", false),
                new FoundryLocalExecutionProvider("CPUExecutionProvider", true),
            ],
            Model = model,
        };
        var progress = new RecordingProgress();
        var provenanceStore = new FakeProvenanceStore();
        using var service = new FoundryLocalSpeechTranscriptionService(sdk, provenanceStore);

        FoundryLocalSpeechPreparationResult result = await service.PrepareAsync(progress);

        Assert.AreEqual(FoundryLocalSpeechPreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(1, sdk.InitializeCalls);
        CollectionAssert.AreEqual(
            new[] { "WinMLExecutionProvider" },
            sdk.RequestedExecutionProviders.ToArray());
        Assert.AreEqual("whisper-tiny", sdk.RequestedAlias);
        Assert.AreEqual(1, model.IsCachedCalls);
        Assert.AreEqual(1, model.DownloadCalls);
        Assert.AreEqual(1, model.LoadCalls);
        Assert.AreEqual(GpuProvenance, service.ModelProvenance);
        Assert.AreEqual(GpuProvenance, provenanceStore.WrittenProvenance);
        Assert.AreEqual(FoundryLocalSpeechReadyState.Ready, service.GetReadyState());
        Assert.AreEqual(1d, progress.Values[^1]);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenAccelerationAcquisitionFails_UsesCachedCpuFallback()
    {
        FoundryLocalModelProvenance cpu = GpuProvenance with
        {
            ResolvedModelId = "whisper-tiny-cpu-v4",
            DeviceType = "CPU",
            ExecutionProvider = "CPUExecutionProvider",
        };
        var model = new FakeSdkModel(cpu) { IsCached = true };
        var sdk = new FakeSdkClient
        {
            Providers = [new FoundryLocalExecutionProvider("WinMLExecutionProvider", false)],
            DownloadExecutionProviderException = new IOException("offline"),
            Model = model,
        };
        using var service = CreateService(sdk);

        FoundryLocalSpeechPreparationResult result = await service.PrepareAsync();

        Assert.AreEqual(FoundryLocalSpeechPreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(0, model.DownloadCalls);
        Assert.AreEqual(1, model.LoadCalls);
        Assert.AreEqual(cpu, service.ModelProvenance);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenAliasIsMissing_ReturnsUnsupported()
    {
        var sdk = new FakeSdkClient { Model = null };
        using var service = CreateService(sdk);

        FoundryLocalSpeechPreparationResult result = await service.PrepareAsync();

        Assert.AreEqual(FoundryLocalSpeechPreparationStatus.Unsupported, result.Status);
        Assert.AreEqual(FoundryLocalSpeechReadyState.NotSupported, service.GetReadyState());
        Assert.IsNull(service.ModelProvenance);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenModelDownloadIsCancelled_ReturnsCancelledWithoutLoading()
    {
        var model = new FakeSdkModel(GpuProvenance)
        {
            IsCached = false,
            DownloadException = new OperationCanceledException(),
        };
        var sdk = new FakeSdkClient { Model = model };
        using var service = CreateService(sdk);

        FoundryLocalSpeechPreparationResult result = await service.PrepareAsync();

        Assert.AreEqual(FoundryLocalSpeechPreparationStatus.Cancelled, result.Status);
        Assert.AreEqual(0, model.LoadCalls);
        Assert.IsNull(service.ModelProvenance);
        Assert.AreEqual(FoundryLocalSpeechReadyState.PreparationNeeded, service.GetReadyState());
    }

    [TestMethod]
    public async Task PrepareAsync_WhenAlreadyReady_ReusesLoadedModel()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        var sdk = new FakeSdkClient { Model = model };
        using var service = CreateService(sdk);

        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);

        Assert.AreEqual(1, sdk.InitializeCalls);
        Assert.AreEqual(1, sdk.GetModelCalls);
        Assert.AreEqual(1, model.LoadCalls);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenProvenanceCacheWriteFails_KeepsLoadedModelReady()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        var sdk = new FakeSdkClient { Model = model };
        var provenanceStore = new FakeProvenanceStore
        {
            WriteException = new IOException("cache unavailable"),
        };
        using var service = new FoundryLocalSpeechTranscriptionService(sdk, provenanceStore);

        FoundryLocalSpeechPreparationResult result = await service.PrepareAsync();

        Assert.AreEqual(FoundryLocalSpeechPreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(FoundryLocalSpeechReadyState.Ready, service.GetReadyState());
        Assert.AreEqual(GpuProvenance, service.ModelProvenance);
        Assert.AreEqual(1, model.LoadCalls);
    }

    [TestMethod]
    public async Task TranscribeAsync_BeforePreparation_ReturnsPreparationRequired()
    {
        var sdk = new FakeSdkClient();
        using var service = CreateService(sdk);

        FoundryLocalTranscriptionResult result = await service.TranscribeAsync(
            new MemoryStream([1, 2, 3], writable: false));

        Assert.AreEqual(FoundryLocalTranscriptionStatus.PreparationRequired, result.Status);
        Assert.AreEqual(0, sdk.GetModelCalls);
    }

    [TestMethod]
    public async Task TranscribeAsync_ChunksWaveAndProducesSourceRelativeSegments()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        model.Transcriptions.Enqueue(new FoundryLocalAudioTranscription(
            " First ",
            [new FoundryLocalAudioTranscriptionSegment(
                "First",
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2))],
            "en"));
        model.Transcriptions.Enqueue(new FoundryLocalAudioTranscription(
            "Second",
            [new FoundryLocalAudioTranscriptionSegment(
                "Second",
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3))],
            "en"));
        model.Transcriptions.Enqueue(new FoundryLocalAudioTranscription(
            "Third",
            [],
            "en"));
        var sdk = new FakeSdkClient { Model = model };
        using var service = CreateService(sdk);
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        await using FileStream audio = File.OpenRead(CreateWaveFile(durationSeconds: 35));
        string sourcePath = audio.Name;
        try
        {
            FoundryLocalTranscriptionResult result = await service.TranscribeAsync(audio);

            Assert.AreEqual(FoundryLocalTranscriptionStatus.Succeeded, result.Status);
            Assert.AreEqual("First\nSecond\nThird", result.Transcript);
            Assert.AreEqual("en", result.LanguageTag);
            Assert.HasCount(3, model.TranscribedPaths);
            CollectionAssert.AreEqual(new[] { "en", "en", "en" },
                model.LanguageHints.ToArray());
            Assert.IsTrue(model.TranscriptionModes.All(mode =>
                mode == FoundryLocalSpeechTranscriptionMode.File));
            Assert.IsTrue(model.NormalizedPcmInputs.All(value => value));
            Assert.HasCount(3, result.Segments!);
            Assert.AreEqual(TimeSpan.FromSeconds(1), result.Segments![0].StartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(17), result.Segments[1].StartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(30), result.Segments[2].StartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(35), result.Segments[2].EndTime);
        }
        finally
        {
            audio.Close();
            TryDelete(sourcePath);
        }
    }

    [TestMethod]
    public async Task WhisperService_UsesSelectedAppLanguageHintAndReportsItAsFallback()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        model.Transcriptions.Enqueue(new FoundryLocalAudioTranscription(
            "bonjour",
            [],
            Language: null));
        var sdk = new FakeSdkClient { Model = model };
        using var service = new FoundryLocalWhisperSpeechTranscriptionService(
            sdk,
            new FakeProvenanceStore(),
            new FixedFoundryLocalSpeechLanguagePolicy("fr"));
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        string path = CreateWaveFile(durationSeconds: 1);
        try
        {
            await using FileStream audio = File.OpenRead(path);
            FoundryLocalTranscriptionResult result = await service.TranscribeAsync(audio);

            Assert.AreEqual(FoundryLocalTranscriptionStatus.Succeeded, result.Status);
            Assert.AreEqual("fr", result.LanguageTag);
            CollectionAssert.AreEqual(new[] { "fr" }, model.LanguageHints.ToArray());
        }
        finally
        {
            TryDelete(path);
        }
    }

    [TestMethod]
    public async Task NemotronService_UsesStableMultilingualAliasAndLivePcmPolicy()
    {
        FoundryLocalModelProvenance provenance = GpuProvenance with
        {
            RequestedAlias = FoundryLocalSpeechModelConfiguration.NemotronMultilingual.ModelAlias,
            ResolvedModelId = "nemotron-multilingual-winml-gpu-v1",
            ModelVersion = "1",
        };
        var model = new FakeSdkModel(provenance) { IsCached = true };
        model.Transcriptions.Enqueue(new FoundryLocalAudioTranscription(
            "bonjour",
            [],
            "fr-FR"));
        var sdk = new FakeSdkClient { Model = model };
        using var service = new FoundryLocalNemotronSpeechTranscriptionService(
            sdk,
            new FakeProvenanceStore(),
            new FixedFoundryLocalSpeechLanguagePolicy("auto"));
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        string path = CreateWaveFile(durationSeconds: 1);
        try
        {
            await using FileStream audio = File.OpenRead(path);
            FoundryLocalTranscriptionResult result = await service.TranscribeAsync(audio);

            Assert.AreEqual(FoundryLocalTranscriptionStatus.Succeeded, result.Status);
            Assert.AreEqual("bonjour", result.Transcript);
            Assert.AreEqual("fr-FR", result.LanguageTag);
            Assert.AreEqual(
                FoundryLocalSpeechModelConfiguration.NemotronMultilingual.ModelAlias,
                sdk.RequestedAlias);
            CollectionAssert.AreEqual(new[] { "auto" }, model.LanguageHints.ToArray());
            CollectionAssert.AreEqual(
                new[] { FoundryLocalSpeechTranscriptionMode.LivePcm },
                model.TranscriptionModes.ToArray());
            CollectionAssert.AreEqual(new[] { true }, model.NormalizedPcmInputs.ToArray());
        }
        finally
        {
            TryDelete(path);
        }
    }

    [TestMethod]
    public async Task NemotronService_WhenCandidateReturnsNoSpeech_RequestsWhisperFallback()
    {
        FoundryLocalModelProvenance provenance = GpuProvenance with
        {
            RequestedAlias = FoundryLocalSpeechModelConfiguration.NemotronMultilingual.ModelAlias,
            ResolvedModelId = "nemotron-multilingual-winml-gpu-v1",
        };
        var model = new FakeSdkModel(provenance) { IsCached = true };
        var sdk = new FakeSdkClient { Model = model };
        using var service = new FoundryLocalNemotronSpeechTranscriptionService(
            sdk,
            new FakeProvenanceStore(),
            new FixedFoundryLocalSpeechLanguagePolicy("auto"));
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        string path = CreateWaveFile(durationSeconds: 1);
        try
        {
            await using FileStream audio = File.OpenRead(path);
            FoundryLocalTranscriptionResult result = await service.TranscribeAsync(audio);

            Assert.AreEqual(FoundryLocalTranscriptionStatus.Unsupported, result.Status);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [TestMethod]
    public async Task Dispose_AfterPreparation_UnloadsModel()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        var sdk = new FakeSdkClient { Model = model };
        var service = CreateService(sdk);
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);

        service.Dispose();

        Assert.AreEqual(1, model.UnloadCalls);
    }

    [TestMethod]
    public async Task ReleaseModelAsync_UnloadsMemoryButPreservesCacheAndProvenance()
    {
        var model = new FakeSdkModel(GpuProvenance) { IsCached = true };
        var sdk = new FakeSdkClient { Model = model };
        using var service = CreateService(sdk);
        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);

        await service.ReleaseModelAsync();

        Assert.AreEqual(1, model.UnloadCalls);
        Assert.AreEqual(GpuProvenance, service.ModelProvenance);
        Assert.AreEqual(
            FoundryLocalSpeechReadyState.PreparationNeeded,
            service.GetReadyState());

        Assert.AreEqual(
            FoundryLocalSpeechPreparationStatus.Succeeded,
            (await service.PrepareAsync()).Status);
        Assert.AreEqual(0, model.DownloadCalls);
        Assert.AreEqual(2, model.LoadCalls);
    }

    private static FoundryLocalSpeechTranscriptionService CreateService(
        IFoundryLocalSdkClient sdkClient,
        FoundryLocalModelProvenance? existingProvenance = null)
    {
        return new FoundryLocalSpeechTranscriptionService(
            sdkClient,
            new FakeProvenanceStore { ExistingProvenance = existingProvenance });
    }

    private static string CreateWaveFile(int durationSeconds)
    {
        const int SampleRate = 1_000;
        const short ChannelCount = 1;
        const short BitsPerSample = 16;
        const short BlockAlignment = ChannelCount * BitsPerSample / 8;
        const int BytesPerSecond = SampleRate * BlockAlignment;
        int dataLength = checked(durationSeconds * BytesPerSecond);
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(BytesPerSecond);
        writer.Write(BlockAlignment);
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Test-created disposable audio is safe for the temp scavenger to remove later.
        }
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }

    private sealed class FakeSdkClient : IFoundryLocalSdkClient
    {
        public IReadOnlyList<FoundryLocalExecutionProvider> Providers { get; init; } = [];

        public IFoundryLocalSdkModel? Model { get; init; }

        public Exception? DownloadExecutionProviderException { get; init; }

        public int InitializeCalls { get; private set; }

        public int DownloadExecutionProviderCalls { get; private set; }

        public int GetModelCalls { get; private set; }

        public string? RequestedAlias { get; private set; }

        public List<string> RequestedExecutionProviders { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public IReadOnlyList<FoundryLocalExecutionProvider> DiscoverExecutionProviders() =>
            Providers;

        public Task<FoundryLocalExecutionProviderDownloadResult>
            DownloadAndRegisterExecutionProvidersAsync(
                IEnumerable<string> providerNames,
                Action<string, double>? progress,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadExecutionProviderCalls++;
            RequestedExecutionProviders.AddRange(providerNames);
            if (DownloadExecutionProviderException != null)
            {
                throw DownloadExecutionProviderException;
            }

            progress?.Invoke(RequestedExecutionProviders.FirstOrDefault() ?? string.Empty, 100);
            return Task.FromResult(new FoundryLocalExecutionProviderDownloadResult(
                true,
                RequestedExecutionProviders.AsReadOnly(),
                []));
        }

        public Task<IFoundryLocalSdkModel?> GetModelAsync(
            string modelAlias,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetModelCalls++;
            RequestedAlias = modelAlias;
            return Task.FromResult(Model);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeProvenanceStore : IFoundryLocalModelProvenanceStore
    {
        public FoundryLocalModelProvenance? ExistingProvenance { get; init; }

        public Exception? WriteException { get; init; }

        public FoundryLocalModelProvenance? WrittenProvenance { get; private set; }

        public FoundryLocalModelProvenance? TryRead(string requestedAlias) =>
            ExistingProvenance?.RequestedAlias == requestedAlias
                ? ExistingProvenance
                : null;

        public void Write(FoundryLocalModelProvenance provenance)
        {
            if (WriteException != null)
            {
                throw WriteException;
            }

            WrittenProvenance = provenance;
        }
    }

    private sealed class FakeSdkModel(FoundryLocalModelProvenance provenance)
        : IFoundryLocalSdkModel
    {
        public FoundryLocalModelProvenance Provenance { get; } = provenance;

        public bool IsCached { get; init; }

        public Exception? DownloadException { get; init; }

        public int IsCachedCalls { get; private set; }

        public int DownloadCalls { get; private set; }

        public int LoadCalls { get; private set; }

        public int UnloadCalls { get; private set; }

        public Queue<FoundryLocalAudioTranscription> Transcriptions { get; } = new();

        public List<string> TranscribedPaths { get; } = [];

        public List<string> LanguageHints { get; } = [];

        public List<FoundryLocalSpeechTranscriptionMode> TranscriptionModes { get; } = [];

        public List<bool> NormalizedPcmInputs { get; } = [];

        public Task<bool> IsCachedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsCachedCalls++;
            return Task.FromResult(IsCached);
        }

        public Task DownloadAsync(Action<float>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadCalls++;
            if (DownloadException != null)
            {
                throw DownloadException;
            }

            progress?.Invoke(100);
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCalls++;
            return Task.CompletedTask;
        }

        public Task UnloadAsync(CancellationToken cancellationToken)
        {
            UnloadCalls++;
            return Task.CompletedTask;
        }

        public Task<FoundryLocalAudioTranscription> TranscribeAsync(
            string audioFilePath,
            string languageHint,
            FoundryLocalSpeechTranscriptionMode transcriptionMode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TranscribedPaths.Add(audioFilePath);
            LanguageHints.Add(languageHint);
            TranscriptionModes.Add(transcriptionMode);
            NormalizedPcmInputs.Add(WavePcm16MonoNormalizer.TryGetPcmDataRange(
                audioFilePath,
                out _,
                out _));
            return Task.FromResult(Transcriptions.Count > 0
                ? Transcriptions.Dequeue()
                : new FoundryLocalAudioTranscription(string.Empty, [], null));
        }
    }
}
