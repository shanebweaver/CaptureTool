using CommunityToolkit.Mvvm.Input;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Drawing;
using Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class WinUICaptureDiscardConfirmationService : ICaptureDiscardConfirmationService
{
    private const double MinimumInlineConfirmationWidth = 360;
    private const double MinimumInlineConfirmationHeight = 220;

    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

    public XamlRoot? XamlRoot { get; set; }
    public Rectangle? DialogHostBounds { get; set; }

    public async Task<bool> ConfirmDiscardActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        string title = _resourceLoader.GetString("CaptureDiscardConfirmation_Title");
        string content = _resourceLoader.GetString("CaptureDiscardConfirmation_Content");
        string discardButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_DiscardButton");
        string cancelButtonText = _resourceLoader.GetString("CaptureDiscardConfirmation_CancelButton");

        bool shouldDiscardCapture = await ShowConfirmationAsync(
            title,
            content,
            discardButtonText,
            cancelButtonText);
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return shouldDiscardCapture;
    }

    private async Task<bool> ShowConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        if (CanShowInlineConfirmation())
        {
            return await ShowInlineConfirmationAsync(
                title,
                content,
                discardButtonText,
                cancelButtonText);
        }

        using var overlayHost = new CaptureDiscardConfirmationOverlayHost(DialogHostBounds);
        return await overlayHost.ShowConfirmationAsync(
            title,
            content,
            discardButtonText,
            cancelButtonText);
    }

    private async Task<bool> ShowInlineConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        XamlRoot? xamlRoot = XamlRoot;
        if (xamlRoot is null)
        {
            return false;
        }

        return await ConfirmationCardPopupPresenter.ShowAsync(
            xamlRoot,
            false,
            complete => new ConfirmationCard
            {
                Title = title,
                Message = content,
                ConfirmButtonText = discardButtonText,
                CancelButtonText = cancelButtonText,
                ConfirmCommand = new RelayCommand(() => complete(true)),
                CancelCommand = new RelayCommand(() => complete(false))
            });
    }

    private bool CanShowInlineConfirmation()
    {
        if (XamlRoot is null)
        {
            return false;
        }

        return XamlRoot.Size.Width >= MinimumInlineConfirmationWidth
            && XamlRoot.Size.Height >= MinimumInlineConfirmationHeight;
    }
}
