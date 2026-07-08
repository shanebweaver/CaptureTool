using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Shell;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    public event EventHandler<INavigationRequest>? NavigationRequested;

    public AppTheme CurrentAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    public AppTheme DefaultAppTheme
    {
        get;
        private set => Set(ref field, value);
    }

    private INavigationRequest? _currentRequest;
    private bool _disposed;

    public MainWindowViewModel(
        IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.CurrentThemeChanged += OnCurrentThemeChanged;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;
    }

    private void OnCurrentThemeChanged(object? sender, AppTheme newTheme)
    {
        CurrentAppTheme = newTheme;
    }

    public bool HandleNavigationRequest(INavigationRequest request)
    {
        if (_currentRequest?.Route == request.Route && _currentRequest?.Parameter == request.Parameter)
        {
            return false;
        }

        _currentRequest = request;
        NavigationRequested?.Invoke(this, request);
        return true;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _themeService.CurrentThemeChanged -= OnCurrentThemeChanged;
            _currentRequest = null;
            NavigationRequested = null;
        }
        finally
        {
            _disposed = true;
            base.Dispose();
        }
    }
}
