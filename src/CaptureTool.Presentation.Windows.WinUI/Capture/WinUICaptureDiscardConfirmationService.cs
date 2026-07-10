using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        var resultCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Popup? popup = null;

        void Complete(bool shouldDiscard)
        {
            resultCompletion.TrySetResult(shouldDiscard);
            if (popup is not null)
            {
                popup.IsOpen = false;
            }
        }

        var root = new Grid
        {
            Width = xamlRoot.Size.Width,
            Height = xamlRoot.Size.Height,
            Background = new SolidColorBrush(Colors.Transparent)
        };

        var card = new ConfirmationCard
        {
            Title = title,
            Message = content,
            ConfirmButtonText = discardButtonText,
            CancelButtonText = cancelButtonText,
            ConfirmCommand = new ActionCommand(() => Complete(true)),
            CancelCommand = new ActionCommand(() => Complete(false))
        };
        root.Children.Add(card);

        void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, root))
            {
                Complete(false);
            }
        }

        void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            root.Width = sender.Size.Width;
            root.Height = sender.Size.Height;
        }

        popup = new Popup
        {
            XamlRoot = xamlRoot,
            Child = root,
            IsLightDismissEnabled = false
        };

        root.PointerPressed += Root_PointerPressed;
        xamlRoot.Changed += XamlRoot_Changed;
        popup.IsOpen = true;

        try
        {
            return await resultCompletion.Task;
        }
        finally
        {
            xamlRoot.Changed -= XamlRoot_Changed;
            root.PointerPressed -= Root_PointerPressed;
            popup.IsOpen = false;
            popup.Child = null;
        }
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
