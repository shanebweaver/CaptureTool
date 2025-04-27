using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Xaml.Views;

public sealed partial class AppMenuView : AppMenuViewBase
{
    public AppMenuView()
    {
        InitializeComponent();
        ViewModel.ShowAboutAppRequested += OnShowAboutAppRequested;
    }

    private void OnShowAboutAppRequested(object? sender, System.EventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            PrimaryButtonText = "Close",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            DefaultButton = ContentDialogButton.Primary,
            Content = new AppAboutView()
        };

        _ = dialog.ShowAsync();
    }
}
