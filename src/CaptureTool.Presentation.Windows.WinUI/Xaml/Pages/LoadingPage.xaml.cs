using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class LoadingPage : LoadingPageBase
{
    public LoadingPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            ViewModel.GoBackCommand.Execute(null);
        }

        base.OnNavigatedTo(e);
    }
}
