using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Presentation.Loading;
using CaptureTool.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public abstract partial class ViewBase<VM> : UserControl where VM : IViewModel
{
    private readonly ILogService _logService = App.Current.ServiceProvider.GetService<ILogService>();
    private CancellationTokenSource? _loadCts;
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
                        _ = asyncLoadable.LoadAsync(_loadCts.Token);
                        break;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logService.LogException(ex, "View load canceled.");
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to load view.");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_loadCts != null)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        ViewModel.Dispose();
    }
}
