using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using CaptureTool.Application.Abstractions.Storage;
using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Foundry.Local.OpenAI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal sealed record FoundryLocalExecutionProvider(
    string Name,
    bool IsRegistered);

internal sealed record FoundryLocalExecutionProviderDownloadResult(
    bool Succeeded,
    IReadOnlyList<string> RegisteredProviders,
    IReadOnlyList<string> FailedProviders);

public sealed record FoundryLocalModelProvenance(
    string RequestedAlias,
    string ResolvedModelId,
    string ModelVersion,
    string DeviceType,
    string ExecutionProvider,
    string CatalogFingerprint);

internal sealed record FoundryLocalAudioTranscription(
    string Text,
    IReadOnlyList<FoundryLocalAudioTranscriptionSegment> Segments,
    string? Language);

internal sealed record FoundryLocalAudioTranscriptionSegment(
    string Text,
    TimeSpan StartTime,
    TimeSpan EndTime);

internal interface IFoundryLocalSdkClient : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);

    IReadOnlyList<FoundryLocalExecutionProvider> DiscoverExecutionProviders();

    Task<FoundryLocalExecutionProviderDownloadResult> DownloadAndRegisterExecutionProvidersAsync(
        IEnumerable<string> providerNames,
        Action<string, double>? progress,
        CancellationToken cancellationToken);

    Task<IFoundryLocalSdkModel?> GetModelAsync(
        string modelAlias,
        CancellationToken cancellationToken);
}

internal interface IFoundryLocalSdkModel
{
    FoundryLocalModelProvenance Provenance { get; }

    Task<bool> IsCachedAsync(CancellationToken cancellationToken);

    Task DownloadAsync(
        Action<float>? progress,
        CancellationToken cancellationToken);

    Task LoadAsync(CancellationToken cancellationToken);

    Task UnloadAsync(CancellationToken cancellationToken);

    Task<FoundryLocalAudioTranscription> TranscribeAsync(
        string audioFilePath,
        string languageHint,
        FoundryLocalSpeechTranscriptionMode transcriptionMode,
        CancellationToken cancellationToken);
}

internal sealed class FoundryLocalSdkClient : IFoundryLocalSdkClient
{
    private readonly IApplicationLocalCachePathProvider _cachePathProvider;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private FoundryLocalManager? _manager;
    private bool _ownsManager;
    private bool _disposed;

    public FoundryLocalSdkClient(IApplicationLocalCachePathProvider cachePathProvider)
    {
        ArgumentNullException.ThrowIfNull(cachePathProvider);
        _cachePathProvider = cachePathProvider;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_manager != null)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_manager != null)
            {
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "Foundry Local WinML requires Windows.");
            }

            if (!FoundryLocalManager.IsInitialized)
            {
                string providerRoot = Path.Combine(
                    _cachePathProvider.GetApplicationLocalCacheFolderPath(),
                    "CaptureAnalysis",
                    "FoundryLocal");
                Directory.CreateDirectory(providerRoot);
                await FoundryLocalManager.CreateAsync(
                    new Configuration
                    {
                        AppName = "capture-tool",
                        AppDataDir = providerRoot,
                        ModelCacheDir = Path.Combine(providerRoot, "models"),
                        LogsDir = Path.Combine(providerRoot, "logs"),
                        LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Fatal,
                    },
                    NullLogger.Instance,
                    cancellationToken).ConfigureAwait(false);
                _ownsManager = true;
            }

            _manager = FoundryLocalManager.Instance;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public IReadOnlyList<FoundryLocalExecutionProvider> DiscoverExecutionProviders()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        FoundryLocalManager manager = GetManager();
        return manager.DiscoverEps()
            .Select(provider => new FoundryLocalExecutionProvider(
                provider.Name,
                provider.IsRegistered))
            .ToArray();
    }

    public async Task<FoundryLocalExecutionProviderDownloadResult>
        DownloadAndRegisterExecutionProvidersAsync(
            IEnumerable<string> providerNames,
            Action<string, double>? progress,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerNames);
        ObjectDisposedException.ThrowIf(_disposed, this);
        string[] names = providerNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (names.Length == 0)
        {
            return new(true, [], []);
        }

        EpDownloadResult result = progress == null
            ? await GetManager()
                .DownloadAndRegisterEpsAsync(names, cancellationToken)
                .ConfigureAwait(false)
            : await GetManager()
                .DownloadAndRegisterEpsAsync(names, progress, cancellationToken)
                .ConfigureAwait(false);
        return new FoundryLocalExecutionProviderDownloadResult(
            result.Success,
            Array.AsReadOnly(result.RegisteredEps ?? []),
            Array.AsReadOnly(result.FailedEps ?? []));
    }

    public async Task<IFoundryLocalSdkModel?> GetModelAsync(
        string modelAlias,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        ObjectDisposedException.ThrowIf(_disposed, this);
        ICatalog catalog = await GetManager()
            .GetCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        IModel? model = await catalog
            .GetModelAsync(modelAlias, cancellationToken)
            .ConfigureAwait(false);
        return model == null
            ? null
            : new FoundryLocalSdkModel(modelAlias, model);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsManager)
        {
            _manager?.Dispose();
        }

        _manager = null;
        _initializationGate.Dispose();
    }

    private FoundryLocalManager GetManager()
    {
        return _manager ?? throw new InvalidOperationException(
            "Foundry Local must be initialized before it is used.");
    }
}

internal sealed class FoundryLocalSdkModel : IFoundryLocalSdkModel
{
    private const int MaximumSegmentCount = 50_000;
    private readonly string _requestedAlias;
    private readonly IModel _model;

    public FoundryLocalSdkModel(string requestedAlias, IModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAlias);
        ArgumentNullException.ThrowIfNull(model);
        _requestedAlias = requestedAlias;
        _model = model;
    }

    public FoundryLocalModelProvenance Provenance
    {
        get
        {
            ModelInfo info = _model.Info;
            string deviceType = info.Runtime?.DeviceType.ToString() ?? "Unknown";
            string executionProvider = string.IsNullOrWhiteSpace(
                info.Runtime?.ExecutionProvider)
                    ? "unknown"
                    : info.Runtime.ExecutionProvider;
            return new FoundryLocalModelProvenance(
                _requestedAlias,
                _model.Id,
                info.Version.ToString(CultureInfo.InvariantCulture),
                deviceType,
                executionProvider,
                ComputeCatalogFingerprint(info));
        }
    }

    public Task<bool> IsCachedAsync(CancellationToken cancellationToken)
    {
        return _model.IsCachedAsync(cancellationToken);
    }

    public Task DownloadAsync(
        Action<float>? progress,
        CancellationToken cancellationToken)
    {
        return _model.DownloadAsync(progress, cancellationToken);
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return _model.LoadAsync(cancellationToken);
    }

    public Task UnloadAsync(CancellationToken cancellationToken)
    {
        return _model.UnloadAsync(cancellationToken);
    }

    public async Task<FoundryLocalAudioTranscription> TranscribeAsync(
        string audioFilePath,
        string languageHint,
        FoundryLocalSpeechTranscriptionMode transcriptionMode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageHint);
        OpenAIAudioClient audioClient = await _model
            .GetAudioClientAsync(cancellationToken)
            .ConfigureAwait(false);
        if (transcriptionMode == FoundryLocalSpeechTranscriptionMode.LivePcm)
        {
            return await TranscribeLivePcmAsync(
                audioClient,
                audioFilePath,
                languageHint,
                cancellationToken).ConfigureAwait(false);
        }

        audioClient.Settings.Language = languageHint;
        audioClient.Settings.Temperature = 0;
        AudioCreateTranscriptionResponse response = await audioClient
            .TranscribeAudioAsync(audioFilePath, cancellationToken)
            .ConfigureAwait(false);
        if (response.Text == null)
        {
            throw new InvalidOperationException(
                "Foundry Local returned an audio response without transcript text.");
        }

        var segments = new List<FoundryLocalAudioTranscriptionSegment>();
        foreach (AudioCreateTranscriptionResponse.Segment segment in response.Segments ?? [])
        {
            if (segments.Count >= MaximumSegmentCount ||
                string.IsNullOrWhiteSpace(segment.Text) ||
                !float.IsFinite(segment.Start) ||
                !float.IsFinite(segment.End) ||
                segment.Start < 0 ||
                segment.End < segment.Start ||
                segment.End > TimeSpan.MaxValue.TotalSeconds)
            {
                continue;
            }

            segments.Add(new FoundryLocalAudioTranscriptionSegment(
                segment.Text,
                TimeSpan.FromSeconds(segment.Start),
                TimeSpan.FromSeconds(segment.End)));
        }

        return new FoundryLocalAudioTranscription(
            response.Text,
            segments.AsReadOnly(),
            response.Language);
    }

    private static async Task<FoundryLocalAudioTranscription> TranscribeLivePcmAsync(
        OpenAIAudioClient audioClient,
        string audioFilePath,
        string languageHint,
        CancellationToken cancellationToken)
    {
        if (!WavePcm16MonoNormalizer.TryGetPcmDataRange(
                audioFilePath,
                out long dataOffset,
                out long dataLength))
        {
            throw new InvalidDataException(
                "Live speech transcription requires 16 kHz, mono, 16-bit PCM wave audio.");
        }

        await using LiveAudioTranscriptionSession session =
            audioClient.CreateLiveTranscriptionSession();
        session.Settings.SampleRate = WavePcm16MonoNormalizer.SampleRate;
        session.Settings.Channels = WavePcm16MonoNormalizer.ChannelCount;
        session.Settings.BitsPerSample = WavePcm16MonoNormalizer.BitsPerSample;
        session.Settings.Language = languageHint;
        session.Settings.PushQueueCapacity = 8;

        var transcriptParts = new List<string>();
        var segments = new List<FoundryLocalAudioTranscriptionSegment>();
        string? language = null;
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        Task responseTask = ConsumeLiveResponsesAsync(
            session,
            transcriptParts,
            segments,
            detectedLanguage => language ??= detectedLanguage,
            streamCancellation.Token);
        try
        {
            await AppendPcmAsync(
                session,
                audioFilePath,
                dataOffset,
                dataLength,
                cancellationToken).ConfigureAwait(false);
            await session.StopAsync(cancellationToken).ConfigureAwait(false);
            await responseTask.ConfigureAwait(false);
        }
        catch
        {
            streamCancellation.Cancel();
            try
            {
                await responseTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The original transcription failure or cancellation is preserved.
            }

            throw;
        }

        return new FoundryLocalAudioTranscription(
            string.Join(' ', transcriptParts),
            segments.AsReadOnly(),
            language);
    }

    private static async Task AppendPcmAsync(
        LiveAudioTranscriptionSession session,
        string audioFilePath,
        long dataOffset,
        long dataLength,
        CancellationToken cancellationToken)
    {
        await using var audio = new FileStream(
            audioFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 32_000,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        audio.Position = dataOffset;
        byte[] buffer = new byte[32_000];
        long remaining = dataLength;
        while (remaining > 0)
        {
            int requested = (int)Math.Min(buffer.Length, remaining);
            int read = await audio.ReadAsync(
                buffer.AsMemory(0, requested),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The normalized audio ended before its declared data boundary.");
            }

            await session.AppendAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
            remaining -= read;
        }
    }

    private static async Task ConsumeLiveResponsesAsync(
        LiveAudioTranscriptionSession session,
        ICollection<string> transcriptParts,
        ICollection<FoundryLocalAudioTranscriptionSegment> segments,
        Action<string?> setLanguage,
        CancellationToken cancellationToken)
    {
        await foreach (LiveAudioTranscriptionResponse response in
            session.GetStream(cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsFinal || segments.Count >= MaximumSegmentCount)
            {
                continue;
            }

            string? text = response.Content?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            string normalizedText = text.Trim();
            transcriptParts.Add(normalizedText);
            setLanguage(string.IsNullOrWhiteSpace(response.Language)
                ? null
                : response.Language);
            if (response.StartTime is double start &&
                response.EndTime is double end &&
                double.IsFinite(start) &&
                double.IsFinite(end) &&
                start >= 0 &&
                end >= start &&
                end <= TimeSpan.MaxValue.TotalSeconds)
            {
                segments.Add(new FoundryLocalAudioTranscriptionSegment(
                    normalizedText,
                    TimeSpan.FromSeconds(start),
                    TimeSpan.FromSeconds(end)));
            }
        }
    }

    private static string ComputeCatalogFingerprint(ModelInfo info)
    {
        var canonical = new StringBuilder();
        Append(canonical, info.Id);
        Append(canonical, info.Name);
        Append(canonical, info.Alias);
        Append(canonical, info.Version.ToString(CultureInfo.InvariantCulture));
        Append(canonical, info.ProviderType);
        Append(canonical, info.Uri);
        Append(canonical, info.ModelType);
        Append(canonical, info.Publisher);
        Append(canonical, info.License);
        Append(canonical, info.LicenseDescription);
        Append(canonical, info.Task);
        Append(canonical, info.MinFLVersion);
        Append(canonical, info.CreatedAtUnix.ToString(CultureInfo.InvariantCulture));
        Append(canonical, info.FileSizeMb?.ToString(CultureInfo.InvariantCulture));
        Append(canonical, info.MaxOutputTokens?.ToString(CultureInfo.InvariantCulture));
        Append(canonical, info.ContextLength?.ToString(CultureInfo.InvariantCulture));
        Append(canonical, info.InputModalities);
        Append(canonical, info.OutputModalities);
        Append(canonical, info.Capabilities);
        Append(canonical, info.SupportsToolCalling?.ToString());
        Append(canonical, info.Runtime?.DeviceType.ToString());
        Append(canonical, info.Runtime?.ExecutionProvider);
        Append(canonical, info.PromptTemplate?.System);
        Append(canonical, info.PromptTemplate?.User);
        Append(canonical, info.PromptTemplate?.Assistant);
        Append(canonical, info.PromptTemplate?.Prompt);
        IEnumerable<Parameter> parameters = info.ModelSettings?.Parameters ?? [];
        foreach (Parameter parameter in parameters
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal))
        {
            Append(canonical, parameter.Name);
            Append(canonical, parameter.Value);
        }

        byte[] fingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"sha256:{Convert.ToHexStringLower(fingerprint)}";
    }

    private static void Append(StringBuilder builder, string? value)
    {
        string normalized = value ?? string.Empty;
        builder.Append(normalized.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(normalized);
    }
}
