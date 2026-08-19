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
    FoundryLocalSpeechReadyState GetReadyState();

    Task<FoundryLocalSpeechPreparationResult> PrepareAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<FoundryLocalTranscriptionResult> TranscribeAsync(
        Stream audio,
        CancellationToken cancellationToken = default);
}

public sealed class FoundryLocalSpeechTranscriptionService :
    IFoundryLocalSpeechTranscriptionService,
    IDisposable
{
    private const int MaximumSegmentCount = 50_000;
    public const string ModelAlias = "whisper-tiny";
    public const string RuntimeVersion = "1.2.3";
    public static readonly TimeSpan MaximumTimestampWindow = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FoundryLocalAudioCommandExecutor _audioCommandExecutor = new();
    private string? _modelId;
    private bool _coreInitialized;
    private bool _unsupported;
    private bool _disposed;

    public FoundryLocalSpeechReadyState GetReadyState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_modelId != null)
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_modelId != null)
            {
                progress?.Report(1);
                return new(FoundryLocalSpeechPreparationStatus.Succeeded);
            }

            if (!_coreInitialized)
            {
                await _audioCommandExecutor.InitializeAsync(cancellationToken)
                    .ConfigureAwait(false);
                _coreInitialized = true;
            }

            string? modelId = await _audioCommandExecutor.FindModelIdAsync(
                ModelAlias,
                "CPU",
                cancellationToken)
                .ConfigureAwait(false);
            if (modelId == null)
            {
                _unsupported = true;
                return new(FoundryLocalSpeechPreparationStatus.Unsupported);
            }

            progress?.Report(0.05);
            await _audioCommandExecutor.DownloadModelAsync(modelId, cancellationToken)
                .ConfigureAwait(false);
            progress?.Report(0.92);
            await _audioCommandExecutor.LoadModelAsync(modelId, cancellationToken)
                .ConfigureAwait(false);

            _modelId = modelId;
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
        string? modelId = _modelId;
        if (modelId == null)
        {
            return new(FoundryLocalTranscriptionStatus.PreparationRequired);
        }

        string temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "CaptureTool",
            "AnalysisAudio");
        string temporaryPath = Path.Combine(temporaryFolder, $"{Guid.NewGuid():N}.wav");
        try
        {
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
                modelId,
                temporaryPath,
                cancellationToken)
                .ConfigureAwait(false);
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
            return new(FoundryLocalTranscriptionStatus.Failed);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // The file contains only an app-created working copy and is safe to retry later.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _modelId = null;
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<FoundryLocalTranscriptionResult> TranscribePreparedWaveAsync(
        string modelId,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        if (!WaveAudioChunkPlan.TryCreate(
                temporaryPath,
                MaximumTimestampWindow,
                out WaveAudioChunkPlan? plan) ||
            plan == null ||
            plan.Chunks.Count == 0)
        {
            FoundryLocalAudioTranscription transcription = await _audioCommandExecutor
                .TranscribeAsync(modelId, temporaryPath, cancellationToken)
                .ConfigureAwait(false);
            return CreateResultWithoutWavePlan(transcription);
        }

        var transcriptParts = new List<string>(plan.Chunks.Count);
        var timestampedSegments = new List<FoundryLocalTranscriptionSegment>(plan.Chunks.Count);
        string? languageTag = null;
        foreach (WaveAudioChunk chunk in plan.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? chunkPath = null;
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

                FoundryLocalAudioTranscription transcription = await _audioCommandExecutor
                    .TranscribeAsync(modelId, analysisPath, cancellationToken)
                    .ConfigureAwait(false);
                string normalizedText = NormalizeTranscript(transcription.Text);
                if (normalizedText.Length > 0)
                {
                    transcriptParts.Add(normalizedText);
                }

                languageTag ??= NormalizeLanguageTag(transcription.Language);
                AddTimestampedSegments(timestampedSegments, transcription, chunk, normalizedText);
            }
            finally
            {
                TryDeleteWorkingFile(chunkPath);
            }
        }

        return new FoundryLocalTranscriptionResult(
            FoundryLocalTranscriptionStatus.Succeeded,
            string.Join('\n', transcriptParts),
            timestampedSegments.AsReadOnly(),
            languageTag);
    }

    private static FoundryLocalTranscriptionResult CreateResultWithoutWavePlan(
        FoundryLocalAudioTranscription transcription)
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
            NormalizeLanguageTag(transcription.Language));
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
