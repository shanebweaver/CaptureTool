using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Presentation.Loading;
using CaptureTool.Presentation.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : IViewModel
{
    private readonly IActiveEditSessionService _activeEditSessionService = App.Current.ServiceProvider.GetService<IActiveEditSessionService>();
    private readonly ILogService _logService = App.Current.ServiceProvider.GetService<ILogService>();
    private readonly INavigationService _navigationService = App.Current.ServiceProvider.GetService<INavigationService>();
    private CancellationTokenSource? _loadCts;

    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public PageBase()
    {
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _loadCts ??= new();

        try
        {
            if (ViewModel is IEditableSession editableSession)
            {
                _activeEditSessionService.SetCurrentSession(editableSession);
            }

            if (ViewModel.IsReadyToLoad)
            {
                switch (ViewModel)
                {
                    case ILoadable loadable:
                        loadable.Load();
                        break;

                    case IAsyncLoadable asyncLoadable:
                        await asyncLoadable.LoadAsync(_loadCts.Token);
                        break;

                    case ILoadableWithParam loadableWithParam:
                        loadableWithParam.Load(e.Parameter);
                        break;

                    case IAsyncLoadableWithParam asyncLoadableWithParam:
                        await asyncLoadableWithParam.LoadAsync(e.Parameter, _loadCts.Token);
                        break;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logService.LogException(ex, "Page load canceled.");
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to load page.");
            await _navigationService.NavigateAsync(NavigationRoute.Error, ex);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_loadCts != null)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        ViewModel.Dispose();
        if (ViewModel is IEditableSession editableSession)
        {
            _activeEditSessionService.ClearCurrentSession(editableSession);
        }

        base.OnNavigatedFrom(e);
    }
}
