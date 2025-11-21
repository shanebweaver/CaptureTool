using CaptureTool.Common;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Themes;

namespace CaptureTool.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, INavigationHandler
{
    private readonly IThemeService _themeService;

    public event EventHandler<INavigationRequest>? NavigationRequested;

    private AppTheme _currentAppTheme;
    public AppTheme CurrentAppTheme
    {
        get => _currentAppTheme;
        set => Set(ref _currentAppTheme, value);
    }

    private AppTheme _defaultAppTheme;
    public AppTheme DefaultAppTheme
    {
        get => _defaultAppTheme;
        set => Set(ref _defaultAppTheme, value);
    }

    private INavigationRequest? _currentRequest;

    public MainWindowViewModel(
        IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.CurrentThemeChanged += OnCurrentThemeChanged;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;
    }

    ~MainWindowViewModel()
    {
        _themeService.CurrentThemeChanged -= OnCurrentThemeChanged;
        _currentRequest = null;
    }

    private void OnCurrentThemeChanged(object? sender, AppTheme newTheme)
    {
        CurrentAppTheme = newTheme;
    }

    public void HandleNavigationRequest(INavigationRequest request)
    {
        if (_currentRequest?.Route == request.Route && _currentRequest?.Parameter == request.Parameter)
        {
            return;
        }

        _currentRequest = request;
        NavigationRequested?.Invoke(this, request);
    }
}
