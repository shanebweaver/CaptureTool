using CaptureTool.Application.Abstractions.TaskEnvironment;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Utils;

/// <summary>Serializes consent dialogs on the main window, including requests from background application services.</summary>
internal sealed class MainWindowDialogCoordinator(ITaskEnvironment ui) : IDisposable
{
    private readonly SemaphoreSlim _dialogs = new(1, 1);
    public XamlRoot? XamlRoot { get; set; }
    public async Task<ContentDialogResult> ShowAsync(Func<ContentDialog> create, CancellationToken ct)
    {
        await _dialogs.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var completion = new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!ui.TryExecute(async () =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    if (XamlRoot == null) throw new InvalidOperationException("The main window is not ready to show consent.");
                    var dialog = create();
                    dialog.XamlRoot = XamlRoot;
                    if (XamlRoot.Content is FrameworkElement root) dialog.RequestedTheme = root.ActualTheme;
                    using var registration = ct.Register(() => ui.TryExecute(() => dialog.Hide()));
                    var result = await dialog.ShowAsync();
                    ct.ThrowIfCancellationRequested();
                    completion.TrySetResult(result);
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(ct); }
                catch (Exception exception) { completion.TrySetException(exception); }
            })) throw new InvalidOperationException("The main window is unavailable.");
            return await completion.Task.ConfigureAwait(false);
        }
        finally { _dialogs.Release(); }
    }
    public void Dispose() => _dialogs.Dispose();
}
