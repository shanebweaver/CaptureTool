using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.AudioCapture;

internal sealed class WinUIAudioCaptureNavigationConfirmationService : IAudioCaptureNavigationConfirmationService
{
    private ResourceLoader? _resourceLoader;

    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmStopActiveRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = GetString("AudioCaptureNavigationConfirmation_Title", "Stop recording?"),
            Content = GetString("AudioCaptureNavigationConfirmation_Content", "Audio recording is currently active."),
            PrimaryButtonText = GetString("AudioCaptureNavigationConfirmation_StopButton", "Stop"),
            CloseButtonText = GetString("AudioCaptureNavigationConfirmation_CancelButton", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }

    private string GetString(string resourceKey, string fallback)
    {
        return WinUIResourceLoader.GetString(ref _resourceLoader, resourceKey, fallback);
    }
}
