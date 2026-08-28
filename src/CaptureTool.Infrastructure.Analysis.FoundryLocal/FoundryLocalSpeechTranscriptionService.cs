using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

public enum FoundryLocalSpeechReadyState
{
    Unknown,
    Ready,
    PreparationNeeded,
    NotSupported,
}

public enum FoundryLocalSpeechPreparationStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    Cancelled,
    Failed,
}

public sealed record FoundryLocalSpeechPreparationResult(
    FoundryLocalSpeechPreparationStatus Status);

public enum FoundryLocalTranscriptionStatus
{
    Unknown,
    Succeeded,
    PreparationRequired,
    Unsupported,
    Cancelled,
    Failed,
}

public sealed record FoundryLocalTranscriptionResult(
    FoundryLocalTranscriptionStatus Status,
    string? Transcript = null,
    IReadOnlyList<FoundryLocalTranscriptionSegment>? Segments = null,
    string? LanguageTag = null);

public sealed record FoundryLocalTranscriptionSegment(
    string Text,
    TimeSpan StartTime,
    TimeSpan EndTime);

public interface IFoundryLocalSpeechTranscriptionService
{
    FoundryLocalModelProvenance? ModelProvenance { get; }

    string LanguageHint { get; }

    FoundryLocalSpeechReadyState GetReadyState();

    Task<FoundryLocalSpeechPreparationResult> PrepareAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<FoundryLocalTranscriptionResult> TranscribeAsync(
        Stream audio,
        CancellationToken cancellationToken = default);

    Task ReleaseModelAsync(CancellationToken cancellationToken = default);
}

internal class FoundryLocalSpeechTranscriptionService :
    IFoundryLocalSpeechTranscriptionService,
    IDisposable
{
    private const int MaximumSegmentCount = 50_000;
    public const string ModelAlias = "whisper-tiny";
    public const string RuntimeVersion = "1.2.4";
    public const string SelectionPolicyRevision =
        "alias-cpu-pcm16-app-language-allowlist-v4";
    public static readonly TimeSpan MaximumTimestampWindow = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IFoundryLocalSdkClient _sdkClient;
    private readonly IFoundryLocalModelProvenanceStore _provenanceStore;
    private readonly FoundryLocalSpeechModelConfiguration _configuration;
    private readonly IFoundryLocalSpeechLanguagePolicy _languagePolicy;
    private IFoundryLocalSdkModel? _model;
    private FoundryLocalModelProvenance? _modelProvenance;
    private bool _unsupported;
    private bool _disposed;

    public FoundryLocalSpeechTranscriptionService(
        IFoundryLocalSdkClient sdkClient,
        IFoundryLocalModelProvenanceStore provenanceStore)
        : this(
            sdkClient,
            provenanceStore,
            FoundryLocalSpeechModelConfiguration.Whisper,
            new FixedFoundryLocalSpeechLanguagePolicy(
                FoundryLocalSpeechModelConfiguration.Whisper.DefaultLanguageHint))
    {
    }

    protected FoundryLocalSpeechTranscriptionService(
        IFoundryLocalSdkClient sdkClient,
        IFoundryLocalModelProvenanceStore provenanceStore,
        FoundryLocalSpeechModelConfiguration configuration,
        IFoundryLocalSpeechLanguagePolicy languagePolicy)
    {
        ArgumentNullException.ThrowIfNull(sdkClient);
        ArgumentNullException.ThrowIfNull(provenanceStore);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(languagePolicy);
        _sdkClient = sdkClient;
        _provenanceStore = provenanceStore;
        _configuration = configuration;
        _languagePolicy = languagePolicy;
        _modelProvenance = provenanceStore.TryRead(configuration.ModelAlias);
    }

    internal FoundryLocalSpeechModelConfiguration Configuration => _configuration;

    public FoundryLocalModelProvenance? ModelProvenance => _modelProvenance;

    public string LanguageHint => _languagePolicy.GetLanguageHint(_configuration);

    public FoundryLocalSpeechReadyState GetReadyState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_model != null)
        {
            return FoundryLocalSpeechReadyState.Ready;
        }

        return _unsupported
            ? FoundryLocalSpeechReadyState.NotSupported
            : FoundryLocalSpeechReadyState.PreparationNeeded;
    }

    public async Task<FoundryLocalSpeechPreparationResult> PrepareAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(FoundryLocalSpeechPreparationStatus.Cancelled);
        }

        try
        {
            if (_model != null)
            {
                progress?.Report(1);
                return new(FoundryLocalSpeechPreparationStatus.Succeeded);
            }

            progress?.Report(0.01);
            await _sdkClient.InitializeAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report(0.03);

            // Foundry's automatic accelerator choice can select a CUDA model that loads
            // successfully but is incompatible with the installed GPU at inference time.
            // Speech uses the catalog's CPU variant for deterministic Store-safe behavior.
            progress?.Report(0.2);

            IFoundryLocalSdkModel? model = await _sdkClient
                .GetModelAsync(
                    _configuration.ModelAlias,
                    _configuration.DevicePreference,
                    cancellationToken)
                .ConfigureAwait(false);
            if (model == null)
            {
                _unsupported = true;
                return new(FoundryLocalSpeechPreparationStatus.Unsupported);
            }

            if (!await model.IsCachedAsync(cancellationToken).ConfigureAwait(false))
            {
                await model.DownloadAsync(
                    percent => progress?.Report(0.2 + (0.7 * ClampPercent(percent))),
                    cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(0.9);
            await model.LoadAsync(cancellationToken).ConfigureAwait(false);

            FoundryLocalModelProvenance provenance = model.Provenance;
            try
            {
                _provenanceStore.Write(provenance);
            }
            catch
            {
                // Exact provenance remains available in memory for this session. A later
                // successful preparation can repair the non-content restart cache.
            }

            _modelProvenance = provenance;
            _model = model;
            _unsupported = false;
            progress?.Report(1);
            return new(FoundryLocalSpeechPreparationStatus.Succeeded);
        }
        catch (OperationCanceledException)
        {
            return new(FoundryLocalSpeechPreparationStatus.Cancelled);
        }
        catch (PlatformNotSupportedException)
        {
            _unsupported = true;
            return new(FoundryLocalSpeechPreparationStatus.Unsupported);
        }
        catch
        {
            return new(FoundryLocalSpeechPreparationStatus.Failed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<FoundryLocalTranscriptionResult> TranscribeAsync(
        Stream audio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(FoundryLocalTranscriptionStatus.Cancelled);
        }

        string? temporaryPath = null;
        try
        {
            string languageHint = LanguageHint;
            IFoundryLocalSdkModel? model = _model;
            if (model == null)
            {
                return new(FoundryLocalTranscriptionStatus.PreparationRequired);
            }

            string temporaryFolder = Path.Combine(
                Path.GetTempPath(),
                "CaptureTool",
                "AnalysisAudio");
            temporaryPath = Path.Combine(temporaryFolder, $"{Guid.NewGuid():N}.wav");
            Directory.CreateDirectory(temporaryFolder);
            await using (var temporary = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await audio.CopyToAsync(temporary, cancellationToken).ConfigureAwait(false);
            }

            return await TranscribePreparedWaveAsync(
                model,
                temporaryPath,
                languageHint,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(FoundryLocalTranscriptionStatus.Cancelled);
        }
        catch (PlatformNotSupportedException)
        {
            return new(FoundryLocalTranscriptionStatus.Unsupported);
        }
        catch
        {
            return new(_configuration.FallbackOnFailure
                ? FoundryLocalTranscriptionStatus.Unsupported
                : FoundryLocalTranscriptionStatus.Failed);
        }
        finally
        {
            TryDeleteWorkingFile(temporaryPath);
            _gate.Release();
        }
    }

    public async Task ReleaseModelAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IFoundryLocalSdkModel? model = _model;
            _model = null;
            if (model != null)
            {
                await model.UnloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IFoundryLocalSdkModel? model = _model;
        _model = null;
        _modelProvenance = null;
        if (model != null)
        {
            try
            {
                model.UnloadAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // App shutdown is best effort; the SDK manager releases remaining resources.
            }
        }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task TryPrepareExecutionProvidersAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            string[] missingProviders = _sdkClient.DiscoverExecutionProviders()
                .Where(provider => !provider.IsRegistered)
                .Select(provider => provider.Name)
                .ToArray();
            if (missingProviders.Length > 0)
            {
                _ = await _sdkClient.DownloadAndRegisterExecutionProvidersAsync(
                    missingProviders,
                    (_, percent) => progress?.Report(0.03 + (0.17 * ClampPercent(percent))),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Acceleration is optional. Re-fetch the catalog and let the SDK choose a
            // compatible registered or cached variant, including its CPU fallback.
        }

        progress?.Report(0.2);
    }

    private async Task<FoundryLocalTranscriptionResult> TranscribePreparedWaveAsync(
        IFoundryLocalSdkModel model,
        string temporaryPath,
        string languageHint,
        CancellationToken cancellationToken)
    {
        if (!WaveAudioChunkPlan.TryCreate(
                temporaryPath,
                FoundryLocalSpeechModelConfiguration.MaximumTimestampWindow,
                out WaveAudioChunkPlan? plan) ||
            plan == null ||
            plan.Chunks.Count == 0)
        {
            if (_configuration.TranscriptionMode == FoundryLocalSpeechTranscriptionMode.LivePcm)
            {
                return new(FoundryLocalTranscriptionStatus.Unsupported);
            }

            FoundryLocalAudioTranscription transcription = await model
                .TranscribeAsync(
                    temporaryPath,
                    languageHint,
                    _configuration.TranscriptionMode,
                    cancellationToken)
                .ConfigureAwait(false);
            return CreateResultWithoutWavePlan(
                transcription,
                GetResultLanguageFallback(languageHint));
        }

        var transcriptParts = new List<string>(plan.Chunks.Count);
        var timestampedSegments = new List<FoundryLocalTranscriptionSegment>(plan.Chunks.Count);
        string? languageTag = null;
        foreach (WaveAudioChunk chunk in plan.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? chunkPath = null;
            string? normalizedPath = null;
            try
            {
                string analysisPath = temporaryPath;
                if (plan.Chunks.Count > 1)
                {
                    chunkPath = Path.Combine(
                        Path.GetDirectoryName(temporaryPath)!,
                        $"{Guid.NewGuid():N}.wav");
                    await plan.WriteChunkAsync(chunk, chunkPath, cancellationToken)
                        .ConfigureAwait(false);
                    analysisPath = chunkPath;
                }

                normalizedPath = Path.Combine(
                    Path.GetDirectoryName(temporaryPath)!,
                    $"{Guid.NewGuid():N}.wav");
                bool normalized = WavePcm16MonoNormalizer.TryNormalize(
                    analysisPath,
                    normalizedPath,
                    cancellationToken);
                if (normalized)
                {
                    analysisPath = normalizedPath;
                }
                else if (_configuration.TranscriptionMode ==
                    FoundryLocalSpeechTranscriptionMode.LivePcm)
                {
                    return new(FoundryLocalTranscriptionStatus.Unsupported);
                }

                FoundryLocalAudioTranscription transcription = await model
                    .TranscribeAsync(
                        analysisPath,
                        languageHint,
                        _configuration.TranscriptionMode,
                        cancellationToken)
                    .ConfigureAwait(false);
                string normalizedText = NormalizeTranscript(transcription.Text);
                if (normalizedText.Length > 0)
                {
                    transcriptParts.Add(normalizedText);
                }

                languageTag ??= NormalizeLanguageTag(transcription.Language) ??
                    GetResultLanguageFallback(languageHint);
                AddTimestampedSegments(timestampedSegments, transcription, chunk, normalizedText);
            }
            finally
            {
                TryDeleteWorkingFile(normalizedPath);
                TryDeleteWorkingFile(chunkPath);
            }
        }

        string transcript = string.Join('\n', transcriptParts);
        if (_configuration.FallbackOnFailure && transcript.Length == 0)
        {
            return new(FoundryLocalTranscriptionStatus.Unsupported);
        }

        return new FoundryLocalTranscriptionResult(
            FoundryLocalTranscriptionStatus.Succeeded,
            transcript,
            timestampedSegments.AsReadOnly(),
            languageTag);
    }

    private static FoundryLocalTranscriptionResult CreateResultWithoutWavePlan(
        FoundryLocalAudioTranscription transcription,
        string? resultLanguageFallback)
    {
        string transcript = NormalizeTranscript(transcription.Text);
        IReadOnlyList<FoundryLocalTranscriptionSegment> segments = transcription.Segments
            .Select(segment => new FoundryLocalTranscriptionSegment(
                NormalizeTranscript(segment.Text),
                segment.StartTime,
                segment.EndTime))
            .Where(segment => segment.Text.Length > 0)
            .ToArray();
        return new FoundryLocalTranscriptionResult(
            FoundryLocalTranscriptionStatus.Succeeded,
            transcript,
            segments,
            NormalizeLanguageTag(transcription.Language) ?? resultLanguageFallback);
    }

    private static void AddTimestampedSegments(
        ICollection<FoundryLocalTranscriptionSegment> destination,
        FoundryLocalAudioTranscription transcription,
        WaveAudioChunk chunk,
        string normalizedChunkText)
    {
        if (destination.Count >= MaximumSegmentCount)
        {
            return;
        }

        int initialCount = destination.Count;
        TimeSpan chunkDuration = chunk.EndTime - chunk.StartTime;
        foreach (FoundryLocalAudioTranscriptionSegment segment in transcription.Segments)
        {
            if (destination.Count >= MaximumSegmentCount)
            {
                break;
            }

            if (segment.StartTime > chunkDuration)
            {
                continue;
            }

            string text = NormalizeTranscript(segment.Text);
            TimeSpan start = chunk.StartTime + segment.StartTime;
            TimeSpan relativeEnd = segment.EndTime > chunkDuration
                ? chunkDuration
                : segment.EndTime;
            TimeSpan end = chunk.StartTime + relativeEnd;
            if (text.Length > 0 && end >= start)
            {
                destination.Add(new FoundryLocalTranscriptionSegment(text, start, end));
            }
        }

        if (destination.Count == initialCount && normalizedChunkText.Length > 0)
        {
            destination.Add(new FoundryLocalTranscriptionSegment(
                normalizedChunkText,
                chunk.StartTime,
                chunk.EndTime));
        }
    }

    private static double ClampPercent(double percent)
    {
        return Math.Clamp(percent, 0, 100) / 100;
    }

    private static string NormalizeTranscript(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
    }

    private static string? NormalizeLanguageTag(string? languageTag)
    {
        return string.IsNullOrWhiteSpace(languageTag)
            ? null
            : languageTag.Trim().Normalize(NormalizationForm.FormC);
    }

    private static string? GetResultLanguageFallback(string languageHint)
    {
        return string.Equals(languageHint, "auto", StringComparison.OrdinalIgnoreCase)
            ? null
            : languageHint;
    }

    private static void TryDeleteWorkingFile(string? path)
    {
        if (path == null)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The path is a uniquely named app-created working copy and is safe to retry later.
        }
    }
}

internal sealed class FoundryLocalWhisperSpeechTranscriptionService :
    FoundryLocalSpeechTranscriptionService
{
    public FoundryLocalWhisperSpeechTranscriptionService(
        IFoundryLocalSdkClient sdkClient,
        IFoundryLocalModelProvenanceStore provenanceStore,
        IFoundryLocalSpeechLanguagePolicy languagePolicy)
        : base(
            sdkClient,
            provenanceStore,
            FoundryLocalSpeechModelConfiguration.Whisper,
            languagePolicy)
    {
    }
}

internal sealed class FoundryLocalNemotronSpeechTranscriptionService :
    FoundryLocalSpeechTranscriptionService
{
    public FoundryLocalNemotronSpeechTranscriptionService(
        IFoundryLocalSdkClient sdkClient,
        IFoundryLocalModelProvenanceStore provenanceStore,
        IFoundryLocalSpeechLanguagePolicy languagePolicy)
        : base(
            sdkClient,
            provenanceStore,
            FoundryLocalSpeechModelConfiguration.NemotronMultilingual,
            languagePolicy)
    {
    }
}
