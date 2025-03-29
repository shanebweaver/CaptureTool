using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public SettingsPageViewModel(
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

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        base.Unload();
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
