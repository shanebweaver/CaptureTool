using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public RelayCommand GoBackCommand => new(GoBack);

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
        Debug.Assert(IsUnloaded);
        StartLoading();

        await Task.Delay(10000, cancellationToken);

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        base.Unload();
    }

    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
