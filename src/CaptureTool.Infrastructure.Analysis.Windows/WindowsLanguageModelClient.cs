using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Logging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Optional Microsoft-issued access credentials for runtimes that require them.</summary>
public sealed record WindowsLanguageModelAccess(string Token, string Attestation);

internal sealed record WindowsLanguageModelResponse(LanguageModelResponseStatus Status, string Text);

internal interface IWindowsLanguageModelSession : IDisposable
{
    Task<WindowsLanguageModelResponse> GenerateAsync(string instructions, string sources);
}

internal interface IWindowsLanguageModelClient
{
    AnalyzerAvailability GetAvailability();
    Task<AnalyzerAvailability> PrepareAsync(CancellationToken ct);
    Task<IWindowsLanguageModelSession> CreateSessionAsync(CancellationToken ct);
}

/// <summary>The WinRT boundary. No native objects are created by registration or passive probes.</summary>
internal sealed class WindowsLanguageModelClient(IAnalysisResources otherModels,
    WindowsLanguageModelAccess? access, ILogService? log = null) : IWindowsLanguageModelClient
{
    public AnalyzerAvailability GetAvailability()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return AnalyzerAvailability.Unsupported;
        }

        try
        {
            AIFeatureReadyState ready = LanguageModel.GetReadyState();
            log?.LogInformation($"Windows language model readiness: {ready}.");
            return MapReady(ready);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogReadinessFailure(exception);
            // Unlock only during an authorized preparation, never a passive probe.
            return access != null && IsAccessDenied(exception) ? AnalyzerAvailability.PreparationRequired : AnalyzerAvailability.Unsupported;
        }
        catch (Exception exception) { LogReadinessFailure(exception); return AnalyzerAvailability.TemporarilyUnavailable; }
    }

    private void LogReadinessFailure(Exception exception) => log?.LogWarning(
        $"Windows language model readiness failed: {exception.GetType().Name} (0x{exception.HResult:X8}).");

    public async Task<AnalyzerAvailability> PrepareAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (access != null)
            {
                var unlocked = LimitedAccessFeatures.TryUnlockFeature("com.microsoft.windows.ai.languagemodel", access.Token, access.Attestation);
                if (unlocked.Status is not (LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken))
                {
                    return AnalyzerAvailability.Unsupported;
                }
            }
            AnalyzerAvailability state = GetAvailability();
            if (state != AnalyzerAvailability.PreparationRequired)
            {
                return state;
            }
            // Keep the operation owned until WinRT completes; cancellation fences subsequent work.
            var result = await LanguageModel.EnsureReadyAsync().AsTask(CancellationToken.None).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return result.Status == AIFeatureReadyResultState.Success ? AnalyzerAvailability.Ready : AnalyzerAvailability.TemporarilyUnavailable;
        }
        catch (Exception exception) when (IsUnavailable(exception)) { return AnalyzerAvailability.Unsupported; }
    }

    public async Task<IWindowsLanguageModelSession> CreateSessionAsync(CancellationToken ct)
    {
        // The vision prerequisite may have left a Foundry model resident. Release it
        // before acquiring the Windows model so both are not retained together.
        await otherModels.ReleaseAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        LanguageModel model = await LanguageModel.CreateAsync().AsTask(CancellationToken.None).ConfigureAwait(false);
        if (ct.IsCancellationRequested) { model.Dispose(); ct.ThrowIfCancellationRequested(); }
        return new Session(model);
    }

    internal static AnalyzerAvailability MapReady(AIFeatureReadyState ready) => ready switch
    {
        AIFeatureReadyState.Ready => AnalyzerAvailability.Ready,
        AIFeatureReadyState.NotReady => AnalyzerAvailability.PreparationRequired,
        AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser or
            AIFeatureReadyState.CapabilityMissing or AIFeatureReadyState.NotCompatibleWithSystemHardware or
            AIFeatureReadyState.OSUpdateNeeded => AnalyzerAvailability.Unsupported,
        _ => AnalyzerAvailability.TemporarilyUnavailable,
    };

    internal static bool IsAccessDenied(Exception exception) => exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005);
    internal static bool IsUnavailable(Exception exception) => exception is TypeLoadException or DllNotFoundException or
        EntryPointNotFoundException or PlatformNotSupportedException or UnauthorizedAccessException ||
        exception is COMException && exception.HResult is unchecked((int)0x80040154) or unchecked((int)0x80004002) or unchecked((int)0x80070005) or unchecked((int)0x80070032);

    private sealed class Session(LanguageModel model) : IWindowsLanguageModelSession
    {
        public async Task<WindowsLanguageModelResponse> GenerateAsync(string instructions, string sources)
        {
            var context = model.CreateContext(instructions);
            if (model.GetUsablePromptLength(context, sources) < (ulong)sources.Length)
            {
                return new(LanguageModelResponseStatus.PromptLargerThanContext, string.Empty);
            }
            // Each request gets a fresh context. The same native instance is used for
            // a bounded correction, then disposed at the end of this explicit action.
            var result = await model.GenerateResponseAsync(context, sources, new LanguageModelOptions { Temperature = 0 })
                .AsTask(CancellationToken.None).ConfigureAwait(false);
            return new(result.Status, result.Text ?? string.Empty);
        }
        public void Dispose() => model.Dispose();
    }
}
