using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.ViewModels.Commands;
using Windows.ApplicationModel;

namespace CaptureTool.ViewModels;

public sealed partial class AppTitleBarViewModel : ViewModelBase
{
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly ITaskEnvironment _taskEnvironment;

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

    public AppTitleBarViewModel(
        ICancellationService cancellationService,
        INavigationService navigationService,
        ITaskEnvironment taskEnvironment)
    {
        _cancellationService = cancellationService;
        _navigationService = navigationService;
        _taskEnvironment = taskEnvironment;
        _icon = "ms-appx:///Assets/StoreLogo.png";
        _title = AppInfo.Current.DisplayInfo.DisplayName;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            CanGoBack = _navigationService.CanGoBack;
            _navigationService.Navigated += OnNavigated;
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    private void OnNavigated(object? sender, NavigationEventArgs e)
    {
        _taskEnvironment.TryExecute(() =>
        {
            CanGoBack = _navigationService.CanGoBack;
        });
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
