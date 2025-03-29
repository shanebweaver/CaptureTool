using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string? _buttonContent;

    public HomePageViewModel(
        IFeatureManager featureManager,
        INavigationService navigationService,
        ISettingsService settingsService)
    {
        _featureManager = featureManager;
        _navigationService = navigationService;
        _settingsService = settingsService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StartLoading();

        bool isAlpha = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Alpha);
        bool isBeta = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Beta);
        bool isClicked = _settingsService.Get(CaptureToolSettings.ButtonClickedSetting);

        string newContent = $"Alpha: {isAlpha}, Beta: {isBeta}, ";
        newContent += isClicked ? "Clicked" : "Click me";

        ButtonContent = newContent;

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
        _settingsService.Set(CaptureToolSettings.ButtonClickedSetting, true);
        await _settingsService.TrySaveAsync();

        ButtonContent = "Clicked";
    }

    [RelayCommand]
    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }
}
