using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class AboutPage : AboutPageBase
{
    private string AppName { get; }
    private string AppVersion { get; }

    public AboutPage()
    {
        InitializeComponent();
        ViewModel.ShowDialogRequested += ViewModel_ShowDialogRequested;

        var package = global::Windows.ApplicationModel.Package.Current;
        var version = package.Id.Version;

        AppName = package.DisplayName;
        AppVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
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
