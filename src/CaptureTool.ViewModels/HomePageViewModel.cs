using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public RelayCommand ClickMeCommand => new(ClickMe);
    public RelayCommand GoToSettingsCommand => new(GoToSettings);

    private string? _buttonContent;
    public string? ButtonContent
    {
        get => _buttonContent;
        set => Set(ref _buttonContent, value);
    }

    public HomePageViewModel(
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        ILocalizationService localizationService,
        INavigationService navigationService,
        ISettingsService settingsService)
    {
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _localizationService = localizationService;
        _navigationService = navigationService;
        _settingsService = settingsService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            bool isAlpha = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Alpha);
            bool isBeta = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Beta);
            bool isClicked = _settingsService.Get(CaptureToolSettings.ButtonClickedSetting);

            string clickedContent = _localizationService.GetString("Clicked");
            string clickMeContent = _localizationService.GetString("ClickMe");

            string newContent = $"Alpha: {isAlpha}, Beta: {isBeta}, ";
            newContent += isClicked ? clickedContent : clickMeContent;

            ButtonContent = newContent;
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ButtonContent = null;
        base.Unload();
    }

    private async void ClickMe()
    {
        var cts = _cancellationService.GetLinkedCancellationTokenSource();

        _settingsService.Set(CaptureToolSettings.ButtonClickedSetting, true);
        await _settingsService.TrySaveAsync(cts.Token);

        string clickedContent = _localizationService.GetString("Clicked");
        ButtonContent = clickedContent;
    }

    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }
}
