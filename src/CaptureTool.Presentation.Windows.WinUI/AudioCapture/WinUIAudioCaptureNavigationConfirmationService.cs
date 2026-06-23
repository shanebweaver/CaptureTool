using CaptureTool.Application.Abstractions.Features.AudioCapture;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.AudioCapture;

internal sealed class WinUIAudioCaptureNavigationConfirmationService : IAudioCaptureNavigationConfirmationService
{
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

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
            Title = _resourceLoader.GetString("AudioCaptureNavigationConfirmation_Title"),
            Content = _resourceLoader.GetString("AudioCaptureNavigationConfirmation_Content"),
            PrimaryButtonText = _resourceLoader.GetString("AudioCaptureNavigationConfirmation_StopButton"),
            CloseButtonText = _resourceLoader.GetString("AudioCaptureNavigationConfirmation_CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return result == ContentDialogResult.Primary;
    }
}
