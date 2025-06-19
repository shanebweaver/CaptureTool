using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class AboutPage : AboutPageBase
{
    public AboutPage()
    {
        InitializeComponent();
        ViewModel.ShowDialogRequested += ViewModel_ShowDialogRequested;
    }

    private void ViewModel_ShowDialogRequested(object? sender, (string title, string content) details)
    {
        ScrollView contentScrollView = new()
        {
            Content = new TextBlock()
            {
                Text = details.content,
                TextWrapping = TextWrapping.WrapWholeWords,
                Padding = new(0,0,12,0)
            }
        };

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = details.title,
            PrimaryButtonText = "Close",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            DefaultButton = ContentDialogButton.Primary,
            Content = contentScrollView
        };

        _ = dialog.ShowAsync();
    }

    //private void OnShowAboutAppRequested(object? sender, System.EventArgs e)
    //{
    //    ContentDialog dialog = new()
    //    {
    //        XamlRoot = XamlRoot,
    //        PrimaryButtonText = "Close",
    //        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
    //        DefaultButton = ContentDialogButton.Primary,
    //        Content = new AppAboutView()
    //    };

    //    _ = dialog.ShowAsync();
    //}
}
