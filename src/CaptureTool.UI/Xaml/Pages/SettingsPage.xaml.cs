using CaptureTool.ViewModels;

namespace CaptureTool.UI.Xaml.Pages;

public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public override SettingsPageViewModel ViewModel { get; } = ViewModelLocator.SettingsPage;
}
