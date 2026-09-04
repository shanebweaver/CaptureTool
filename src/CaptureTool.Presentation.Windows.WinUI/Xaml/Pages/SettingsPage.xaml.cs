using CaptureTool.Presentation.Features.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;

#if DEBUG
        LocalizationSection.Visibility = Visibility.Visible;
#endif
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        SettingsAmbientMotionStoryboard.Begin();
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

    private void AudioAutoCopyToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateAudioCaptureAutoCopyCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void AudioAutoSaveToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateAudioCaptureAutoSaveCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void AudioDefaultLocalAudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateAudioCaptureDefaultLocalAudioCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void VideoDefaultLocalAudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateVideoCaptureDefaultLocalAudioCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void CaptureWarnBeforeDiscardToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateCaptureWarnBeforeDiscardCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void EditWarnBeforeDiscardToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateEditWarnBeforeDiscardCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void StoreReviewRemindersToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateStoreReviewRemindersEnabledCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private async void CaptureMemoryAnalysisToggleSwitch_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggleSwitch)
        {
            return;
        }

        await ViewModel.CaptureMemory.SetAnalyzingNewCapturesAsync(toggleSwitch.IsOn);
        toggleSwitch.IsOn = ViewModel.CaptureMemory.IsAnalyzingNewCaptures;
    }

    private void OptionalUsageDataToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            _ = ViewModel.UpdateOptionalUsageDataEnabledCommand.ExecuteAsync(toggleSwitch.IsOn);
        }
    }

    private void AiFeatureConsentCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is AiFeatureConsentViewModel featureConsent)
        {
            _ = ViewModel.UpdateAiFeatureConsentAsync(featureConsent.FeatureId, checkBox.IsChecked == true);
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
