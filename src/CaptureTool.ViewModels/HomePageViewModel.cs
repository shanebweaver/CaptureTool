using System;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string? _buttonContent;

    public HomePageViewModel(
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        INavigationService navigationService,
        ISettingsService settingsService)
    {
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _navigationService = navigationService;
        _settingsService = settingsService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            bool isAlpha = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Alpha);
            bool isBeta = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Beta);
            bool isClicked = _settingsService.Get(CaptureToolSettings.ButtonClickedSetting);

            string newContent = $"Alpha: {isAlpha}, Beta: {isBeta}, ";
            newContent += isClicked ? "Clicked" : "Click me";

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

    [RelayCommand]
    private async Task ClickMe()
    {
        var cts = _cancellationService.GetLinkedCancellationTokenSource();

        _settingsService.Set(CaptureToolSettings.ButtonClickedSetting, true);
        await _settingsService.TrySaveAsync(cts.Token);

        ButtonContent = "Clicked";
    }

    [RelayCommand]
    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }
}
