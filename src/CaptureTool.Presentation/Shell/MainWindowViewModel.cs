using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shell;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAppNotificationService _notificationService;

    public event EventHandler<INavigationRequest>? NavigationRequested;

    public IRelayCommand DismissNotificationCommand { get; }

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

    public AppNotification? CurrentNotification => _notificationService.CurrentNotification;

    public bool HasNotification => _notificationService.HasNotification;

    public string NotificationMessage => CurrentNotification?.Message ?? string.Empty;

    public bool IsCurrentNotificationError => CurrentNotification?.Kind == AppNotificationKind.Error;

    public bool IsCurrentNotificationInfo => CurrentNotification?.Kind == AppNotificationKind.Info;

    private INavigationRequest? _currentRequest;
    private bool _disposed;

    public MainWindowViewModel(
        IThemeService themeService,
        IAppNotificationService notificationService)
    {
        _themeService = themeService;
        _notificationService = notificationService;
        _themeService.CurrentThemeChanged += OnCurrentThemeChanged;
        _notificationService.PropertyChanged += OnNotificationServicePropertyChanged;
        DefaultAppTheme = _themeService.DefaultTheme;
        CurrentAppTheme = _themeService.CurrentTheme;
        DismissNotificationCommand = new RelayCommand(_notificationService.DismissCurrent);
    }

    private void OnCurrentThemeChanged(object? sender, AppTheme newTheme)
    {
        CurrentAppTheme = newTheme;
    }

    private void OnNotificationServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppNotificationService.CurrentNotification) or
            nameof(IAppNotificationService.HasNotification) or
            nameof(IAppNotificationService.NotificationCount))
        {
            RaisePropertyChanged(nameof(CurrentNotification));
            RaisePropertyChanged(nameof(HasNotification));
            RaisePropertyChanged(nameof(NotificationMessage));
            RaisePropertyChanged(nameof(IsCurrentNotificationError));
            RaisePropertyChanged(nameof(IsCurrentNotificationInfo));
        }
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
            _notificationService.PropertyChanged -= OnNotificationServicePropertyChanged;
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
