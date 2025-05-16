using CaptureTool.Common.Loading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;

namespace CaptureTool.UI.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
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
            if (ViewModel is ILoadable loadable && loadable.IsUnloaded)
            {
                await loadable.LoadAsync(e.Parameter, _loadCts.Token);
            }
        }
        catch (OperationCanceledException ex)
        {
            ServiceLocator.Logging.LogException(ex, "Page load canceled.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Logging.LogException(ex, "Failed to load page.");
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

        if (ViewModel is ILoadable loadable && loadable.IsLoaded)
        {
            loadable.Unload();
        }

        base.OnNavigatedFrom(e);
    }
}
