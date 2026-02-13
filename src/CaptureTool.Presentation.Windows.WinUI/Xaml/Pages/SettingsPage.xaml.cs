using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

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

    private void VideoAutoCopyToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateVideoCaptureAutoCopyCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void VideoAutoSaveToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateVideoCaptureAutoSaveCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void VideoDefaultLocalAudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateVideoCaptureDefaultLocalAudioCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void MetadataAutoSaveToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateVideoMetadataAutoSaveCommand.ExecuteAsync(toggleSwitch.IsOn);
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
            _ = ViewModel.UpdateAppLanguageCommand.ExecuteAsync(radioButtons.SelectedIndex);
        }
    }
}
