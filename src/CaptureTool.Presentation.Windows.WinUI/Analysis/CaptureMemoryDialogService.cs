using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Analysis;

internal sealed class CaptureMemoryDialogService(ITaskEnvironment ui) : ICaptureMemoryPrompts, IDisposable
{
    private readonly SemaphoreSlim _dialogs = new(1, 1);
    private ResourceLoader? _resources;
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken cancellationToken)
    {
        await _dialogs.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!ui.TryExecute(async () =>
            {
                try
                {
                    if (XamlRoot == null || cancellationToken.IsCancellationRequested) { completion.TrySetResult(false); return; }
                    string name = prompt.ToString();
                    var dialog = new ContentDialog
                    {
                        XamlRoot = XamlRoot,
                        Title = Text("CaptureMemory_" + name + "Title"),
                        Content = Text("CaptureMemory_" + name + "Content"),
                        PrimaryButtonText = Text("CaptureMemory_" + name + "Accept"),
                        CloseButtonText = Text("CaptureMemory_Cancel"),
                        DefaultButton = prompt == CaptureMemoryPrompt.DeleteMetadata ? ContentDialogButton.Close : ContentDialogButton.Primary
                    };
                    AutomationProperties.SetAutomationId(dialog, "CaptureMemory" + name + "Dialog");
                    // Keep a managed callback target; passing the WinRT method group
                    // directly fails delegate marshalling when cancellation closes it.
                    using var registration = cancellationToken.Register(() => ui.TryExecute(() => dialog.Hide()));
                    ContentDialogResult result = await dialog.ShowAsync();
                    completion.TrySetResult(!cancellationToken.IsCancellationRequested && result == ContentDialogResult.Primary);
                }
                catch (Exception exception) { completion.TrySetException(exception); }
            })) return false;
            return await completion.Task.ConfigureAwait(false);
        }
        finally { _dialogs.Release(); }
    }
    private string Text(string key) => WinUIResourceLoader.GetString(ref _resources, key, key);
    public void Dispose() => _dialogs.Dispose();
}
