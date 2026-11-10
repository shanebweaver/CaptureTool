using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal static class UiTestInferenceLifetime
{
    internal static async Task WaitAsync(string source, ILogService log, CancellationToken cancellationToken)
    {
        if (UiTestLaunchOptions.AiProfile is not ("shutdown-cancel" or "shutdown-late-result")) return;

        log.LogInformation($"UI test inference started: {source}.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            log.LogInformation($"UI test inference canceled: {source}.");
            // Simulate native provider cleanup and a completion queued on the UI
            // dispatcher. Exit must wait for it, even if it returns a late result.
            await Task.Delay(250);
            log.LogInformation($"UI test inference drained: {source}.");
            if (UiTestLaunchOptions.AiProfile != "shutdown-late-result") throw;
        }
    }
}
