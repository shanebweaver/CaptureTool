using CaptureTool.Common;
using CaptureTool.Common.Loading;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
    private CancellationTokenSource? _loadCts;
    private readonly ILogService _logService;
    private readonly IAppNavigation _appNavigation;

    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public PageBase()
    {
        DataContext = ViewModel;
        _logService = App.Current.ServiceProvider.GetService<ILogService>();
        _appNavigation = App.Current.ServiceProvider.GetService<IAppNavigation>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _loadCts ??= new();

        try
        {
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
            _appNavigation.GoToError(ex);
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

        base.OnNavigatedFrom(e);
    }
}
