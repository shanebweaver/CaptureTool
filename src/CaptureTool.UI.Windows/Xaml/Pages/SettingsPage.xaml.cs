namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();

#if DEBUG
        LocalizationSection.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
#endif
    }
}
