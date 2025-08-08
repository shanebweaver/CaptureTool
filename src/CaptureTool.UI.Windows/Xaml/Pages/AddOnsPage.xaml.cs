using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.System;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class AddOnsPage : AddOnsPageBase
{
    public AddOnsPage()
    {
        InitializeComponent();
    }

    private void ShowChromaKeyImageFlipViewDialog()
    {
        FlipView flipView = new();
        flipView.Items.Add(new Image() { Source = ChromaKeyImageOff.Source });
        flipView.Items.Add(new Image() { Source = ChromaKeyImageOn.Source });

        string closeButtonText = new ResourceLoader().GetString("ContentDialog_Close");
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            PrimaryButtonText = closeButtonText,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            DefaultButton = ContentDialogButton.Primary,
            Content = flipView,
        };

        _ = dialog.ShowAsync();
    }

    private void FlipViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ShowChromaKeyImageFlipViewDialog();
    }

    private void FlipViewItem_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
            case VirtualKey.Space:
                ShowChromaKeyImageFlipViewDialog();
                break;
        }
    }
}