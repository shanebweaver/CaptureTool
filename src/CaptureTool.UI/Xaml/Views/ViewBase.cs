using System;
using System.Threading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.UI.Xaml.Views;

public abstract partial class ViewBase<VM> : UserControl where VM : ViewModelBase
{
    private readonly CancellationTokenSource _loadCts = new();
    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public ViewBase()
    {
        DataContext = ViewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    ~ViewBase()
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.IsUnloaded)
            {
                _ = ViewModel.LoadAsync(null, _loadCts.Token);
            }
        }
        catch (OperationCanceledException ex)
        {
            ServiceLocator.Logging.LogException(ex, "View load canceled.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Logging.LogException(ex, "Failed to load view.");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCts.Cancel();
        _loadCts.Dispose();

        ViewModel.Unload();
    }
}
