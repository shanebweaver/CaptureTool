using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Navigation;
using Windows.ApplicationModel;

namespace CaptureTool.ViewModels;

public sealed partial class AppTitleBarViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public RelayCommand GoBackCommand => new(GoBack);

    private bool _canGoBack;
    public bool CanGoBack
    {
        get => _canGoBack;
        set => Set(ref _canGoBack, value);
    }

    private string? _icon;
    public string? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    private string? _title;
    public string? Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public AppTitleBarViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _icon = "ms-appx:///Assets/StoreLogo.png";
        _title = AppInfo.Current.DisplayInfo.DisplayName;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        CanGoBack = _navigationService.CanGoBack;
        _navigationService.Navigated += OnNavigated;

        return base.LoadAsync(parameter, cancellationToken);
    }

    private void OnNavigated(object? sender, NavigationEventArgs e)
    {
        CanGoBack = _navigationService.CanGoBack;
    }

    public override void Unload()
    {
        _navigationService.Navigated -= OnNavigated;
        Icon = null;
        Title = null;
        CanGoBack = false;
        base.Unload();
    }

    private void GoBack()
    {
        Debug.Assert(_navigationService.CanGoBack);
        _navigationService.GoBack();
    }
}
