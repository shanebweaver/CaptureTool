using CaptureTool.Presentation.Windows.WinUI.Xaml.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.System;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class AboutPage : AboutPageBase
{
    private string AppName { get; }
    private string AppVersion { get; }

    public AboutPage()
    {
        InitializeComponent();
        Loaded += AboutPage_Loaded;
        ViewModel.ShowDialogRequested += ViewModel_ShowDialogRequested;

        var package = global::Windows.ApplicationModel.Package.Current;
        var version = package.Id.Version;

        AppName = package.DisplayName;
        AppVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    private void AboutPage_Loaded(object sender, RoutedEventArgs e)
    {
        AboutAmbientMotionStoryboard.Begin();
    }

    private async void ExternalLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string destination }
            && Uri.TryCreate(destination, UriKind.Absolute, out Uri? uri))
        {
            _ = await Launcher.LaunchUriAsync(uri);
        }
    }

    private void ViewModel_ShowDialogRequested(object? sender, (string title, string content) details)
    {
        ScrollView contentScrollView = new()
        {
            Content = new TextBlock()
            {
                Text = details.content,
                TextWrapping = TextWrapping.WrapWholeWords,
                Padding = new(0, 0, 12, 0)
            }
        };

        string closeButtonText = new ResourceLoader().GetString("ContentDialog_Close");
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = details.title,
            PrimaryButtonText = closeButtonText,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            DefaultButton = ContentDialogButton.Primary,
            Content = contentScrollView
        };

        _ = dialog.ShowAsync();
    }

    private void AppNameAndVersionTextBlock_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        string closeButtonText = new ResourceLoader().GetString("ContentDialog_Close");
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = new ResourceLoader().GetString("Diagnostics_Title"),
            PrimaryButtonText = closeButtonText,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            DefaultButton = ContentDialogButton.Primary,
            Content = new DiagnosticsView()
        };

        _ = dialog.ShowAsync();
    }
}
