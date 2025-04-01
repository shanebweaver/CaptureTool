using System;
using System.Threading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
    private readonly CancellationTokenSource _loadCts = new();

    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public PageBase()
    {
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            if (ViewModel.IsUnloaded)
            {
                _ = ViewModel.LoadAsync(e.Parameter, _loadCts.Token);
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
        _loadCts.Cancel();
        _loadCts.Dispose();

        if (e.NavigationMode == NavigationMode.Back)
        {
            ViewModel.Unload();
        }

        base.OnNavigatedFrom(e);
    }
}
