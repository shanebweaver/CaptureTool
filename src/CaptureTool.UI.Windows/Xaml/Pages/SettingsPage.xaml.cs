using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();

#if DEBUG
        LocalizationSection.Visibility = Visibility.Visible;
#endif
    }

    private void ImageAutoCopyToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateImageCaptureAutoCopyCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void ImageAutoSaveToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateImageCaptureAutoSaveCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void AppThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadioButtons radioButtons)
        {
            ViewModel.UpdateAppThemeCommand.Execute(radioButtons.SelectedIndex);
        }
    }

    private void AppLanguageRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadioButtons radioButtons)
        {
            ViewModel.UpdateAppLanguageCommand.Execute(radioButtons.SelectedIndex);
        }
    }
}
