using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Foundry.Local.OpenAI;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed class FoundrySpeechAnalyzer(string id, string alias, bool streaming, FoundryRuntime runtime,
    WindowsAnalysisMedia media) : IMediaAnalyzer
{
    private const int MaximumSegments = 50000;
    private const int MaximumCharacters = 2 * 1024 * 1024;
    private IModel? _model;
    public MediaAnalyzerDescriptor Descriptor { get; } = new(id, AnalysisCapability.Transcription, [AnalysisMediaKind.Audio, AnalysisMediaKind.Video]);

    public async ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind mediaKind, string? language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mediaKind is not (AnalysisMediaKind.Audio or AnalysisMediaKind.Video) ||
            !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64) || !SupportsLanguage(language))
            return AnalyzerAvailability.Unsupported;
        return _model != null && await _model.IsCachedAsync(cancellationToken).ConfigureAwait(false)
            ? AnalyzerAvailability.Ready : AnalyzerAvailability.PreparationRequired;
    }

    public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        _model = await runtime.ResolveAsync(alias, cancellationToken).ConfigureAwait(false);
        if (_model == null) return AnalyzerAvailability.Unsupported;
        if (!await _model.IsCachedAsync(cancellationToken).ConfigureAwait(false))
            await _model.DownloadAsync(value => progress?.Report(new(AnalysisProgressStage.Preparing,
                double.IsFinite(value) ? Math.Clamp(value / 100d, 0, 1) : null)), cancellationToken).ConfigureAwait(false);
        progress?.Report(new(AnalysisProgressStage.Preparing, 1));
        return AnalyzerAvailability.Ready;
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        try { return await AnalyzeCoreAsync(input, progress, cancellationToken).ConfigureAwait(false); }
        catch (InvalidAnalysisMediaException) { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "invalid-media"); }
    }

    private async Task<AnalyzerOutcome> AnalyzeCoreAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        if (!SupportsLanguage(input.Language)) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-language");
        IModel? model = _model;
        if (model == null) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.TemporarilyUnavailable, "model-not-prepared");
        try
        {
            await model.LoadAsync(cancellationToken).ConfigureAwait(false);
            OpenAIAudioClient client = await model.GetAudioClientAsync(cancellationToken).ConfigureAwait(false);
            var transcript = new TranscriptCollector();
            string? language = null;
            await foreach (AudioChunk chunk in media.ReadAudioChunksAsync(input.SourcePath, input.MediaKind, cancellationToken).ConfigureAwait(false))
            {
                ReadOnlyMemory<byte> samples = PcmWave.GetSamples(await File.ReadAllBytesAsync(chunk.Path, cancellationToken).ConfigureAwait(false));
                // Digital silence has no speech. Do not let a model invent a transcript;
                // preserve even the quietest nonzero sample without a VAD threshold.
                if (samples.Span.IndexOfAnyExcept((byte)0) < 0)
                {
                    progress?.Report(new(AnalysisProgressStage.Analyzing));
                    continue;
                }
                if (streaming)
                {
                    string? detectedLanguage = await TranscribeLiveAsync(client, chunk, samples, transcript, cancellationToken).ConfigureAwait(false);
                    language ??= detectedLanguage;
                }
                else
                {
                    client.Settings.Language = string.IsNullOrWhiteSpace(input.Language) ? "auto" : NeutralLanguage(input.Language);
                    client.Settings.Temperature = 0;
                    var response = await client.TranscribeAudioAsync(chunk.Path, cancellationToken).ConfigureAwait(false);
                    if (!response.Successful || response.Text == null) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "invalid-transcript");
                    language ??= response.Language;
                    if (!string.IsNullOrWhiteSpace(response.Text))
                        transcript.Add(response.Text.Trim(), chunk.Offset, chunk.Offset + chunk.Duration);
                }
                progress?.Report(new(AnalysisProgressStage.Analyzing));
            }
            return AnalyzerOutcome.Success(new TranscriptMetadata(string.IsNullOrWhiteSpace(language) ? null : language,
                transcript.Segments.OrderBy(segment => segment.Start)), new(id, "foundry-local", model.Id, "1", model.Info.Version.ToString(CultureInfo.InvariantCulture)));
        }
        finally { await model.UnloadAsync(CancellationToken.None).ConfigureAwait(false); }
    }

    private static async Task<string?> TranscribeLiveAsync(OpenAIAudioClient client, AudioChunk chunk, ReadOnlyMemory<byte> samples,
        TranscriptCollector transcript, CancellationToken ct)
    {
        await using LiveAudioTranscriptionSession session = client.CreateLiveTranscriptionSession();
        session.Settings.SampleRate = 16000;
        session.Settings.Channels = 1;
        session.Settings.BitsPerSample = 16;
        session.Settings.Language = "auto";
        session.Settings.PushQueueCapacity = 8;
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await session.StartAsync(ct).ConfigureAwait(false);
        string? language = null;
        Task consume = ConsumeAsync();
        try
        {
            // Feed 100 ms PCM blocks. SDK 1.2.4 truncates utterance tails with
            // one-second pushes, even after StopAsync completes.
            const int blockBytes = 16000 * 2 / 10;
            for (int offset = 0; offset < samples.Length; offset += blockBytes)
                await session.AppendAsync(samples.Slice(offset, Math.Min(blockBytes, samples.Length - offset)), streamCancellation.Token).ConfigureAwait(false);
            await session.StopAsync(streamCancellation.Token).ConfigureAwait(false);
            await consume.ConfigureAwait(false);
        }
        finally
        {
            streamCancellation.Cancel();
            try { await consume.ConfigureAwait(false); } catch (OperationCanceledException) when (streamCancellation.IsCancellationRequested) { }
        }
        return language;

        async Task ConsumeAsync()
        {
            try
            {
                await foreach (LiveAudioTranscriptionResponse response in session.GetStream(streamCancellation.Token).ConfigureAwait(false))
                {
                    if (!response.IsFinal) continue;
                    string? text = response.Content?.FirstOrDefault()?.Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    double start = response.StartTime is { } s && double.IsFinite(s) ? Math.Clamp(s, 0, chunk.Duration.TotalSeconds) : 0;
                    double end = response.EndTime is { } e && double.IsFinite(e) ? Math.Clamp(e, start, chunk.Duration.TotalSeconds) : chunk.Duration.TotalSeconds;
                    transcript.Add(text.Trim(), chunk.Offset + TimeSpan.FromSeconds(start), chunk.Offset + TimeSpan.FromSeconds(end));
                    language ??= response.Language;
                }
            }
            catch { streamCancellation.Cancel(); throw; }
        }
    }

    private bool SupportsLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return true;
        string neutral = NeutralLanguage(language);
        // Published Nemotron locales and Whisper tiny's 99 language tokens (Whisper tokenizer.py).
        return streaming ? " ar bg cs da de el en es et fi fr he hi hr hu it ja ko lt lv mt nl no pl pt ro ru sk sl sv th tr uk vi zh ".Contains(" " + neutral + " ", StringComparison.Ordinal)
            : (" en zh de es ru ko fr ja pt tr pl ca nl ar sv it id hi fi vi he uk el ms cs ro da hu ta no th ur hr bg lt la mi ml cy sk te fa lv bn sr az sl kn et mk br eu is hy ne mn bs kk sq sw gl mr pa si km sn yo so af oc ka be tg sd gu am yi lo uz fo ht ps tk nn mt sa lb my bo tl mg as tt haw ln ha ba jw su ")
                .Contains(" " + neutral + " ", StringComparison.Ordinal);
    }
    private static string NeutralLanguage(string language) => language.Split(['-', '_'])[0].ToLowerInvariant();

    private sealed class TranscriptCollector
    {
        private int _characters;
        public List<TranscriptSegment> Segments { get; } = [];
        public void Add(string text, TimeSpan start, TimeSpan end)
        {
            if (Segments.Count >= MaximumSegments || text.Length > MaximumCharacters - _characters)
                throw new InvalidDataException("Transcript limit exceeded.");
            _characters += text.Length;
            Segments.Add(new(text, start, end));
        }
    }
}
