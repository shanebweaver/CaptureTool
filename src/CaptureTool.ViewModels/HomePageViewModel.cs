using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private static readonly BoolSettingDefinition buttonClickedSetting = new("ButtonClicked", false);

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string? _buttonContent;

    public HomePageViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        StartLoading();

        bool isClicked = _settingsService.Get(buttonClickedSetting);
        ButtonContent = isClicked ? "Clicked" : "Click me";

        return base.LoadAsync(parameter, cancellationToken);
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
