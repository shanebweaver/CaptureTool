using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Drawing;
using Windows.System;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class WinUIMicrophoneAccessPrompt : IMicrophoneAccessPrompt, IDisposable
{
    private readonly SemaphoreSlim _dialogs = new(1, 1);
    private ResourceLoader? _resources;
    public Rectangle? DialogHostBounds { get; set; }

    public async Task ShowAsync(CancellationToken cancellationToken = default)
    {
        await _dialogs.WaitAsync(cancellationToken);
        try
        {
            // A capture toolbar may be too small to host a dialog. Use the same
            // monitor-sized, capture-excluded surface as discard confirmation.
            using var host = new CaptureConfirmationOverlayHost(DialogHostBounds);
            bool openSettings = await host.ShowConfirmationAsync(
                Text("MicrophoneAccess_Title", "Allow microphone access"),
                Text("MicrophoneAccess_Content", "Windows is blocking microphone access. Open microphone privacy settings to allow access for CaptureTool or desktop apps, then return and try again. You can also mute the microphone to record without it."),
                Text("MicrophoneAccess_OpenSettings", "Open microphone settings"),
                Text("CaptureDiscardConfirmation_CancelButton", "Cancel"),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (openSettings && !await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-microphone")))
            {
                throw new InvalidOperationException("Windows microphone settings could not be opened.");
            }
        }
        finally
        {
            _dialogs.Release();
        }
    }

    private string Text(string key, string fallback) => WinUIResourceLoader.GetString(ref _resources, key, fallback);
    public void Dispose() => _dialogs.Dispose();
}
