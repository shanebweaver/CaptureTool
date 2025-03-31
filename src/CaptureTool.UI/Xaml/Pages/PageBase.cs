using System;
using System.Threading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

public abstract class PageBase<VM> : Page where VM : ViewModelBase
{
    private readonly CancellationTokenSource _navigationCts = new();

    public abstract VM ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            if (ViewModel.IsUnloaded)
            {
                _ = ViewModel.LoadAsync(e.Parameter, _navigationCts.Token);
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
        _navigationCts.Cancel();
        _navigationCts.Dispose();

        if (e.NavigationMode == NavigationMode.Back)
        {
            ViewModel.Unload();
        }

        base.OnNavigatedFrom(e);
    }
}
