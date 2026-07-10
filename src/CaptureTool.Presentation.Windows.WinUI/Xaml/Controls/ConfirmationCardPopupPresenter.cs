using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

internal static class ConfirmationCardPopupPresenter
{
    public static async Task<TResult> ShowAsync<TResult>(
        XamlRoot xamlRoot,
        TResult dismissResult,
        Func<Action<TResult>, ConfirmationCard> createCard)
    {
        var resultCompletion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Popup? popup = null;

        void Complete(TResult result)
        {
            resultCompletion.TrySetResult(result);
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
        root.Children.Add(createCard(Complete));

        void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, root))
            {
                Complete(dismissResult);
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
}
