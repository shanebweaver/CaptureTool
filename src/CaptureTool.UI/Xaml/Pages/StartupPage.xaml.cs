using CaptureTool.ViewModels;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class StartupPage : StartupPageBase
{
    public StartupPage()
    {
        InitializeComponent();
    }

    public override StartupPageViewModel ViewModel { get; } = ViewModelLocator.StartupPage;
}
