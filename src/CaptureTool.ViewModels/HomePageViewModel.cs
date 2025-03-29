using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Settings.Definitions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private static readonly BoolSettingDefinition buttonClickedSetting = new("ButtonClicked", false);

    private readonly IFeatureManager _featureManager;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string? _buttonContent;

    public HomePageViewModel(
        IFeatureManager featureManager,
        ISettingsService settingsService)
    {
        _featureManager = featureManager;
        _settingsService = settingsService;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StartLoading();

        bool isAlpha = await _featureManager.IsEnabledAsync(Features.Feature_Alpha);
        bool isBeta = await _featureManager.IsEnabledAsync(Features.Feature_Beta);
        bool isClicked = _settingsService.Get(buttonClickedSetting);

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
    private void ClickMe()
    {
        _settingsService.Set(buttonClickedSetting, true);
        ButtonContent = "Clicked";
    }
}
