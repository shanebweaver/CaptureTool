using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Speech;
using System.Runtime.InteropServices;

#pragma warning disable CS8305 // This adapter intentionally isolates an experimental Windows API.

namespace CaptureTool.Infrastructure.Analysis.Windows;

public enum WindowsAiSpeechReadyState
{
    Unknown,
    Ready,
    PreparationNeeded,
    NotSupported,
    Disabled,
}

public enum WindowsAiSpeechPreparationStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    Disabled,
    Cancelled,
    Failed,
}

public sealed record WindowsAiSpeechPreparationResult(
    WindowsAiSpeechPreparationStatus Status);

public enum WindowsAiSpeechTranscriptionStatus
{
    Unknown,
    Succeeded,
    PreparationRequired,
    Unsupported,
    Disabled,
    Cancelled,
    Failed,
}

public sealed record WindowsAiSpeechTranscriptionSegment(
    string Text,
    TimeSpan StartTime,
    TimeSpan EndTime);

public sealed record WindowsAiSpeechTranscriptionResult(
    WindowsAiSpeechTranscriptionStatus Status,
    string? Transcript = null,
    IReadOnlyList<WindowsAiSpeechTranscriptionSegment>? Segments = null);

public interface IWindowsAiSpeechRecognitionService
{
    WindowsAiSpeechReadyState GetReadyState();

    Task<WindowsAiSpeechPreparationResult> PrepareAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<WindowsAiSpeechTranscriptionResult> TranscribeAsync(
        Stream audio,
        CancellationToken cancellationToken = default);
}

public sealed class WindowsAiSpeechRecognitionService :
    IWindowsAiSpeechRecognitionService
{
    private const int MaximumSegmentCount = 50_000;
    public const string ModelId = "windows-ai-speech-recognition";
    public static readonly TimeSpan MaximumTimestampWindow = TimeSpan.FromSeconds(15);
    public static readonly string? RuntimeVersion =
        typeof(SpeechRecognitionModel).Assembly.GetName().Version?.ToString();

    public WindowsAiSpeechReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(SpeechRecognitionModel.GetReadyState());
        }
        catch
        {
            return WindowsAiSpeechReadyState.Unknown;
        }
    }

    public async Task<WindowsAiSpeechPreparationResult> PrepareAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        WindowsAiSpeechReadyState state = GetReadyState();
        if (state == WindowsAiSpeechReadyState.Ready)
        {
            progress?.Report(1);
            return new(WindowsAiSpeechPreparationStatus.Succeeded);
        }

        if (state == WindowsAiSpeechReadyState.NotSupported)
        {
            return new(WindowsAiSpeechPreparationStatus.Unsupported);
        }

        if (state == WindowsAiSpeechReadyState.Disabled)
        {
            return new(WindowsAiSpeechPreparationStatus.Disabled);
        }

        try
        {
            var providerProgress = progress == null
                ? null
                : new DelegateProgress<SpeechRecognitionModelProgress>(value =>
                    progress.Report(Math.Clamp(value.Progress, 0, 1)));
            AIFeatureReadyResult result = providerProgress == null
                ? await SpeechRecognitionModel.EnsureReadyAsync()
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false)
                : await SpeechRecognitionModel.EnsureReadyAsync()
                    .AsTask(cancellationToken, providerProgress)
                    .ConfigureAwait(false);
            if (result.Status == AIFeatureReadyResultState.Success)
            {
                progress?.Report(1);
                return new(WindowsAiSpeechPreparationStatus.Succeeded);
            }

            return new(WindowsAiSpeechPreparationStatus.Failed);
        }
        catch (OperationCanceledException)
        {
            return new(WindowsAiSpeechPreparationStatus.Cancelled);
        }
        catch (COMException)
        {
            return new(WindowsAiSpeechPreparationStatus.Failed);
        }
        catch (PlatformNotSupportedException)
        {
            return new(WindowsAiSpeechPreparationStatus.Unsupported);
        }
        catch
        {
            return new(WindowsAiSpeechPreparationStatus.Failed);
        }
    }

    public async Task<WindowsAiSpeechTranscriptionResult> TranscribeAsync(
        Stream audio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        WindowsAiSpeechReadyState state = GetReadyState();
        if (state != WindowsAiSpeechReadyState.Ready)
        {
            return state switch
            {
                WindowsAiSpeechReadyState.PreparationNeeded =>
                    new(WindowsAiSpeechTranscriptionStatus.PreparationRequired),
                WindowsAiSpeechReadyState.NotSupported =>
                    new(WindowsAiSpeechTranscriptionStatus.Unsupported),
                WindowsAiSpeechReadyState.Disabled =>
                    new(WindowsAiSpeechTranscriptionStatus.Disabled),
                _ => new(WindowsAiSpeechTranscriptionStatus.Failed),
            };
        }

        string sourcePath = WindowsVideoAnalysisWorkingFiles.CreatePath(".wav");
        try
        {
            await WindowsVideoAnalysisWorkingFiles.CopyToNewFileAsync(
                audio,
                sourcePath,
                cancellationToken).ConfigureAwait(false);

            SpeechRecognitionModelResult modelResult = await SpeechRecognitionModel
                .TryCreateAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            if (modelResult.SpeechModel == null)
            {
                return new(WindowsAiSpeechTranscriptionStatus.Failed);
            }

            using SpeechRecognitionModel model = modelResult.SpeechModel;
            using var recognition = new BatchRecognition(model);
            return await TranscribePreparedWaveAsync(
                recognition,
                sourcePath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(WindowsAiSpeechTranscriptionStatus.Cancelled);
        }
        catch (PlatformNotSupportedException)
        {
            return new(WindowsAiSpeechTranscriptionStatus.Unsupported);
        }
        catch (COMException)
        {
            return new(WindowsAiSpeechTranscriptionStatus.Failed);
        }
        catch
        {
            return new(WindowsAiSpeechTranscriptionStatus.Failed);
        }
        finally
        {
            WindowsVideoAnalysisWorkingFiles.TryDelete(sourcePath);
        }
    }

    internal static WindowsAiSpeechReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => WindowsAiSpeechReadyState.Ready,
            AIFeatureReadyState.NotReady => WindowsAiSpeechReadyState.PreparationNeeded,
            AIFeatureReadyState.DisabledByUser => WindowsAiSpeechReadyState.Disabled,
            AIFeatureReadyState.NotSupportedOnCurrentSystem or
            AIFeatureReadyState.NotCompatibleWithSystemHardware or
            AIFeatureReadyState.CapabilityMissing or
            AIFeatureReadyState.OSUpdateNeeded => WindowsAiSpeechReadyState.NotSupported,
            _ => WindowsAiSpeechReadyState.Unknown,
        };
    }

    private static async Task<WindowsAiSpeechTranscriptionResult> TranscribePreparedWaveAsync(
        BatchRecognition recognition,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        if (!WaveAudioChunkPlan.TryCreate(
                sourcePath,
                MaximumTimestampWindow,
                out WaveAudioChunkPlan? plan) ||
            plan == null ||
            plan.Chunks.Count == 0)
        {
            string transcript = Normalize(await recognition
                .RecognizeFromFile(sourcePath)
                .AsTask(cancellationToken)
                .ConfigureAwait(false));
            return new(
                WindowsAiSpeechTranscriptionStatus.Succeeded,
                transcript,
                []);
        }

        var parts = new List<string>(plan.Chunks.Count);
        var segments = new List<WindowsAiSpeechTranscriptionSegment>(plan.Chunks.Count);
        foreach (WaveAudioChunk chunk in plan.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? chunkPath = null;
            try
            {
                string analysisPath = sourcePath;
                if (plan.Chunks.Count > 1)
                {
                    chunkPath = WindowsVideoAnalysisWorkingFiles.CreatePath(".wav");
                    await plan.WriteChunkAsync(chunk, chunkPath, cancellationToken)
                        .ConfigureAwait(false);
                    analysisPath = chunkPath;
                }

                string text = Normalize(await recognition
                    .RecognizeFromFile(analysisPath)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false));
                if (text.Length > 0)
                {
                    parts.Add(text);
                    if (segments.Count < MaximumSegmentCount)
                    {
                        segments.Add(new WindowsAiSpeechTranscriptionSegment(
                            text,
                            chunk.StartTime,
                            chunk.EndTime));
                    }
                }
            }
            finally
            {
                WindowsVideoAnalysisWorkingFiles.TryDelete(chunkPath);
            }
        }

        return new(
            WindowsAiSpeechTranscriptionStatus.Succeeded,
            string.Join('\n', parts),
            segments.AsReadOnly());
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(System.Text.NormalizationForm.FormC);

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
